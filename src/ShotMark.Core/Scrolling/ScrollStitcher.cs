namespace ShotMark.Core.Scrolling;

public enum StitchStatus { First, Appended, NoMovement, NoMatch }

public sealed record StitchResult(StitchStatus Status, int Shift, int AddedRows);

public sealed record StitchOptions
{
    public int LeftMargin { get; init; } = 8;
    public int RightMargin { get; init; } = 28;
    public int Samples { get; init; } = 64;
    public int Tolerance { get; init; } = 12;
    public int MaxDifferentSamples { get; init; } = 2;
    public double MinMatchRatio { get; init; } = 0.97;
    public double MinOverlapFraction { get; init; } = 0.25;
    public int MinOverlapRows { get; init; } = 40;
    public double MaxStaticFraction { get; init; } = 0.4;

    /// <summary>Knappere Übereinstimmung, die nur gilt, wenn die Verschiebung eindeutig besser ist als jede andere.</summary>
    public double RelaxedMatchRatio { get; init; } = 0.75;

    public double RelaxedMargin { get; init; } = 0.15;

    /// <summary>Anteil der Überlappung oben und unten, der beim Vergleich unberücksichtigt bleibt (sich ändernde Kopf/Fußzeilen).</summary>
    public double EdgeFraction { get; init; } = 1.0 / 6;

    public int MaxEdgeRows { get; init; } = 120;

    /// <summary>Weniger auswertbare Stichproben in einer Zeile: Zeile sagt nichts aus (z. B. weiße Fläche).</summary>
    public int MinInformativeSamples { get; init; } = 8;

    /// <summary>Mindestübereinstimmung an gleicher Position, damit ein Teilbild den letzten Stand ersetzen darf.</summary>
    public double RebaseMinRatio { get; init; } = 0.6;
}

/// <summary>
/// Setzt beim Scrollen aufgenommene Teilbilder zu einem langen Bild zusammen.
/// Kopfzeilen kommen aus dem ersten, Fußzeilen aus dem letzten Teilbild – dadurch ist das Ergebnis auch dann
/// pixelgenau, wenn zufällig gleiche Zeilen (z. B. weiße Ränder) für „fest“ gehalten werden.
/// Bildteile, die in zwei Teilbildern an derselben Bildschirmstelle gleich bleiben (schwebende Knöpfe, feste
/// Kopfzeilen), werden beim Suchen der Verschiebung ausgeblendet.
/// </summary>
public sealed class ScrollStitcher(StitchOptions? options = null)
{
    private enum RowCompare { Match, Mismatch, Neutral }

    private readonly StitchOptions _o = options ?? new StitchOptions();
    private readonly List<byte[]> _header = [];
    private readonly List<byte[]> _content = [];
    private readonly List<byte[]> _footer = [];
    private PixelFrame? _previous;
    private byte[][] _previousSignatures = [];
    private int[] _sampleX = [];
    private int _left;
    private int _right;
    private bool _bandsKnown;
    private int _top;
    private int _bottom;
    private int _lastShift;
    private int[] _columnChanges = [];
    private int _matchedFrames;

    public int Width => _previous?.Width ?? 0;
    public int Height => _header.Count + _content.Count + _footer.Count;
    public int BandHeight => _previous is null ? 0 : _previous.Height - _top - _bottom;

    /// <summary>Breite des Ergebnisses ohne erkannten Scrollbalken am rechten Rand.</summary>
    public int OutputWidth
    {
        get
        {
            if (_previous is null || _matchedFrames == 0) return Width;
            var firstColumn = Width - _columnChanges.Length;
            for (var i = 0; i < _columnChanges.Length; i++)
                if (_columnChanges[i] > 0 && _columnChanges[i] * 2 >= _matchedFrames)
                    return Math.Max(1, firstColumn + i);
            return Width;
        }
    }

    public StitchResult Add(PixelFrame frame)
    {
        if (_previous is null)
        {
            Initialize(frame);
            return new StitchResult(StitchStatus.First, 0, frame.Height);
        }
        EnsureSameSize(frame);

        var signatures = Signatures(frame);
        var unmoved = MatchRatio(_previousSignatures, signatures, _bandsKnown ? _top : 0, _bandsKnown ? _bottom : 0);
        if (unmoved >= _o.MinMatchRatio)
            return new StitchResult(StitchStatus.NoMovement, 0, 0);
        // Zeigt das Bild weitgehend dieselbe Stelle, wirkt alles „fest“ – dann nur der strenge Abgleich
        var tolerant = unmoved < _o.RebaseMinRatio;

        if (!_bandsKnown)
        {
            var maxStatic = (int)(frame.Height * _o.MaxStaticFraction);
            var top = CountEqualRows(_previousSignatures, signatures, fromTop: true);
            var bottom = CountEqualRows(_previousSignatures, signatures, fromTop: false);
            _top = top <= maxStatic ? top : 0;
            _bottom = bottom <= maxStatic ? bottom : 0;
            var shift = FindShiftAndVerify(_previous, frame, _previousSignatures, signatures, tolerant);
            if (shift is null && (_top > 0 || _bottom > 0))
            {
                _top = _bottom = 0;
                shift = FindShiftAndVerify(_previous, frame, _previousSignatures, signatures, tolerant);
            }
            if (shift is not { } found)
            {
                _top = _bottom = 0;
                return new StitchResult(StitchStatus.NoMatch, 0, 0);
            }
            SplitFirstFrame();
            _bandsKnown = true;
            return Append(frame, signatures, found);
        }

        return FindShiftAndVerify(_previous, frame, _previousSignatures, signatures, tolerant) is { } dy
            ? Append(frame, signatures, dy)
            : new StitchResult(StitchStatus.NoMatch, 0, 0);
    }

    /// <summary>
    /// Ersetzt den zuletzt übernommenen Stand durch <paramref name="frame"/>, wenn es dieselbe Scrollposition zeigt,
    /// sich aber teilweise verändert hat (z. B. nachgeladene Bilder). Liefert false, wenn es nicht passt.
    /// </summary>
    public bool TryRebase(PixelFrame frame)
    {
        if (_previous is null || frame.Width != _previous.Width || frame.Height != _previous.Height) return false;
        var signatures = Signatures(frame);
        if (MatchRatio(_previousSignatures, signatures, _top, _bottom) < _o.RebaseMinRatio) return false;
        var band = BandHeight;
        if (_content.Count < band) return false;
        for (var i = 0; i < band; i++) _content[_content.Count - band + i] = frame.Row(_top + i).ToArray();
        ReplaceFooter(frame);
        _previous = frame;
        _previousSignatures = signatures;
        return true;
    }

    public PixelFrame Build()
    {
        if (_previous is null) throw new InvalidOperationException("Noch kein Teilbild aufgenommen.");
        var width = OutputWidth;
        var rowBytes = width * 4;
        var rows = _header.Concat(_content).Concat(_footer).ToList();
        var pixels = new byte[rowBytes * rows.Count];
        for (var y = 0; y < rows.Count; y++)
            rows[y].AsSpan(0, rowBytes).CopyTo(pixels.AsSpan(y * rowBytes));
        return new PixelFrame(width, rows.Count, pixels);
    }

    private void EnsureSameSize(PixelFrame frame)
    {
        if (frame.Width != _previous!.Width || frame.Height != _previous.Height)
            throw new ArgumentException("Alle Teilbilder müssen gleich groß sein.", nameof(frame));
    }

    private void Initialize(PixelFrame frame)
    {
        _previous = frame;
        var usable = frame.Width - _o.LeftMargin - _o.RightMargin;
        (_left, _right) = usable >= 16 ? (_o.LeftMargin, frame.Width - _o.RightMargin) : (0, frame.Width);
        var count = Math.Min(_o.Samples, _right - _left);
        _sampleX = Enumerable.Range(0, count).Select(i => _left + (int)((long)i * (_right - _left) / count)).ToArray();
        _columnChanges = new int[Math.Min(frame.Width, _o.RightMargin)];
        _previousSignatures = Signatures(frame);
        for (var y = 0; y < frame.Height; y++) _content.Add(frame.Row(y).ToArray());
    }

    /// <summary>Erstes Teilbild in feste Kopfzeilen, Inhalt und (vorläufige) Fußzeilen aufteilen.</summary>
    private void SplitFirstFrame()
    {
        var all = _content.ToList();
        _content.Clear();
        _header.AddRange(all.Take(_top));
        _content.AddRange(all.Skip(_top).Take(all.Count - _top - _bottom));
        _footer.AddRange(all.Skip(all.Count - _bottom));
    }

    private StitchResult Append(PixelFrame frame, byte[][] signatures, int dy)
    {
        var bandEnd = frame.Height - _bottom;
        for (var y = bandEnd - dy; y < bandEnd; y++) _content.Add(frame.Row(y).ToArray());
        ReplaceFooter(frame);
        TrackRightColumns(_previous!, frame, _previousSignatures, signatures, dy);
        _previous = frame;
        _previousSignatures = signatures;
        _lastShift = dy;
        return new StitchResult(StitchStatus.Appended, dy, dy);
    }

    private void ReplaceFooter(PixelFrame frame)
    {
        _footer.Clear();
        for (var y = frame.Height - _bottom; y < frame.Height; y++) _footer.Add(frame.Row(y).ToArray());
    }

    private byte[][] Signatures(PixelFrame frame)
    {
        var result = new byte[frame.Height][];
        var pixels = frame.Pixels;
        for (var y = 0; y < frame.Height; y++)
        {
            var signature = new byte[_sampleX.Length];
            var rowStart = y * frame.Stride;
            for (var s = 0; s < _sampleX.Length; s++)
            {
                var i = rowStart + _sampleX[s] * 4;
                signature[s] = (byte)((pixels[i] * 29 + pixels[i + 1] * 150 + pixels[i + 2] * 77) >> 8);
            }
            result[y] = signature;
        }
        return result;
    }

    private bool RowsMatch(byte[] a, byte[] b)
    {
        var different = 0;
        for (var i = 0; i < a.Length; i++)
            if (Math.Abs(a[i] - b[i]) > _o.Tolerance && ++different > _o.MaxDifferentSamples)
                return false;
        return true;
    }

    /// <summary>
    /// Vergleicht Zeile <paramref name="yPrevious"/> des alten mit Zeile <paramref name="yCurrent"/> des neuen Teilbilds.
    /// Eine abweichende Stichprobe zählt nicht, wenn an derselben Bildschirmstelle beide Teilbilder gleich sind –
    /// dort sitzt ein festes Element (schwebender Knopf, feste Kopfzeile), das nicht mitscrollt.
    /// </summary>
    private RowCompare Compare(byte[][] previous, byte[][] current, int yPrevious, int yCurrent)
    {
        var a = previous[yPrevious];
        var b = current[yCurrent];
        int considered = 0, different = 0;
        for (var i = 0; i < a.Length; i++)
        {
            if (Math.Abs(a[i] - b[i]) <= _o.Tolerance)
            {
                considered++;
                continue;
            }
            if (Math.Abs(previous[yCurrent][i] - b[i]) <= _o.Tolerance || Math.Abs(a[i] - current[yPrevious][i]) <= _o.Tolerance)
                continue;
            considered++;
            different++;
        }
        if (considered < Math.Min(_o.MinInformativeSamples, a.Length)) return RowCompare.Neutral;
        return different <= _o.MaxDifferentSamples ? RowCompare.Match : RowCompare.Mismatch;
    }

    /// <summary>Übereinstimmung bei Verschiebung 0 (gleiche Bildschirmposition), ohne Ausblenden.</summary>
    private double MatchRatio(byte[][] previous, byte[][] current, int top, int bottom)
    {
        var rows = previous.Length - top - bottom;
        if (rows <= 0) return 0;
        var matches = 0;
        for (var y = top; y < top + rows; y++)
            if (RowsMatch(previous[y], current[y])) matches++;
        return matches / (double)rows;
    }

    private int CountEqualRows(byte[][] previous, byte[][] current, bool fromTop)
    {
        var count = 0;
        for (var i = 0; i < previous.Length; i++)
        {
            var y = fromTop ? i : previous.Length - 1 - i;
            if (!RowsMatch(previous[y], current[y])) break;
            count++;
        }
        return count;
    }

    private int? FindShiftAndVerify(PixelFrame previous, PixelFrame current, byte[][] prevSignatures, byte[][] curSignatures, bool allowTolerant)
    {
        // 1. Mit Ausblenden fester Elemente und toleranten Rändern
        if (allowTolerant && FindTolerantShift(prevSignatures, curSignatures) is { } tolerant
            && Verify(previous, current, tolerant.Shift, tolerant.Strict ? _o.MinMatchRatio : _o.RelaxedMatchRatio, tolerant: true))
            return tolerant.Shift;
        // 2. Rückfall: strenger Vergleich aller Zeilen (z. B. wenn kaum auswertbare Zeilen übrig bleiben)
        if (FindStrictShift(prevSignatures, curSignatures) is { } strict
            && Verify(previous, current, strict, _o.MinMatchRatio, tolerant: false))
            return strict;
        return null;
    }

    private (int Shift, bool Strict)? FindTolerantShift(byte[][] previous, byte[][] current)
    {
        var band = previous.Length - _top - _bottom;
        if (band < 2) return null;
        var minOverlap = MinOverlap(band);
        var candidates = new List<(int Dy, double Ratio, int Evidence)>();
        for (var dy = 1; dy <= band - minOverlap; dy++)
        {
            var overlap = band - dy;
            var edge = Edge(overlap);
            var core = overlap - 2 * edge;
            if (core <= 0) continue;
            var allowed = core - (int)Math.Ceiling(core * _o.RelaxedMatchRatio);
            int informative = 0, misses = 0;
            for (var i = edge; i < overlap - edge && misses <= allowed; i++)
            {
                switch (Compare(previous, current, _top + i + dy, _top + i))
                {
                    case RowCompare.Match:
                        informative++;
                        break;
                    case RowCompare.Mismatch:
                        informative++;
                        misses++;
                        break;
                }
            }
            if (misses > allowed || informative < Math.Max(4, core / 4)) continue;
            candidates.Add((dy, 1 - misses / (double)informative, informative));
        }
        if (candidates.Count == 0) return null;

        var best = candidates[0];
        foreach (var candidate in candidates.Skip(1))
        {
            // Gleich gute Übereinstimmung: mehr Belegzeilen gewinnen, dann die Nähe zum letzten Schritt
            var tie = Math.Abs(candidate.Ratio - best.Ratio) <= 1e-9;
            if (candidate.Ratio > best.Ratio + 1e-9
                || (tie && candidate.Evidence > best.Evidence)
                || (tie && candidate.Evidence == best.Evidence && IsBetterTie(candidate.Dy, best.Dy)))
                best = candidate;
        }
        if (best.Ratio >= _o.MinMatchRatio) return (best.Dy, true);
        var runnerUp = candidates.Where(c => Math.Abs(c.Dy - best.Dy) > 3).Select(c => c.Ratio).DefaultIfEmpty(0).Max();
        return best.Ratio >= _o.RelaxedMatchRatio && best.Ratio - runnerUp >= _o.RelaxedMargin ? (best.Dy, false) : null;
    }

    private int? FindStrictShift(byte[][] previous, byte[][] current)
    {
        var band = previous.Length - _top - _bottom;
        if (band < 2) return null;
        var minOverlap = MinOverlap(band);
        int? best = null;
        var bestRatio = 0.0;
        for (var dy = 1; dy <= band - minOverlap; dy++)
        {
            var overlap = band - dy;
            var allowed = overlap - (int)Math.Ceiling(overlap * _o.MinMatchRatio);
            var misses = 0;
            for (var i = 0; i < overlap && misses <= allowed; i++)
                if (!RowsMatch(previous[_top + i + dy], current[_top + i])) misses++;
            if (misses > allowed) continue;
            var ratio = 1 - misses / (double)overlap;
            if (best is null || ratio > bestRatio + 1e-9 || (Math.Abs(ratio - bestRatio) <= 1e-9 && IsBetterTie(dy, best.Value)))
            {
                best = dy;
                bestRatio = ratio;
            }
        }
        return best;
    }

    private int MinOverlap(int band) => Math.Min(band - 1, Math.Max(_o.MinOverlapRows, (int)(band * _o.MinOverlapFraction)));

    private int Edge(int overlap) => Math.Min((int)(overlap * _o.EdgeFraction), _o.MaxEdgeRows);

    // Bei gleich guter Übereinstimmung (z. B. Leerflächen) die Verschiebung nahe dem letzten Schritt bevorzugen
    private bool IsBetterTie(int candidate, int current) =>
        _lastShift > 0 ? Math.Abs(candidate - _lastShift) < Math.Abs(current - _lastShift) : candidate < current;

    /// <summary>
    /// Kandidaten Pixel für Pixel nachprüfen (Stichprobe von höchstens ~200 Zeilen). Im toleranten Modus zählen
    /// Pixel nicht, die an derselben Bildschirmstelle in beiden Teilbildern gleich sind, und die Ränder der
    /// Überlappung bleiben unberücksichtigt.
    /// </summary>
    private bool Verify(PixelFrame previous, PixelFrame current, int dy, double threshold, bool tolerant)
    {
        var overlap = previous.Height - _top - _bottom - dy;
        if (overlap <= 0) return false;
        var edge = tolerant ? Edge(overlap) : 0;
        var core = overlap - 2 * edge;
        if (core <= 0) return false;
        var step = Math.Max(1, core / 200);
        int checkedRows = 0, goodRows = 0;
        for (var i = edge; i < overlap - edge; i += step)
        {
            var yPrevious = _top + i + dy;
            var yCurrent = _top + i;
            int considered = 0, differences = 0;
            for (var x = _left; x < _right; x++)
            {
                if (SamePixel(previous, yPrevious, current, yCurrent, x))
                {
                    considered++;
                    continue;
                }
                if (tolerant && (SamePixel(previous, yCurrent, current, yCurrent, x) || SamePixel(previous, yPrevious, current, yPrevious, x)))
                    continue;
                considered++;
                differences++;
            }
            if (considered < 16) continue;
            checkedRows++;
            if (differences <= Math.Max(1, considered / 100)) goodRows++;
        }
        if (checkedRows == 0) return !tolerant;
        // Kleiner Spielraum, weil die Stichprobe der Pixelprüfung nicht genau den Signaturzeilen entspricht
        return goodRows >= checkedRows * (threshold - 0.03);
    }

    private bool SamePixel(PixelFrame a, int rowA, PixelFrame b, int rowB, int x)
    {
        var ia = rowA * a.Stride + x * 4;
        var ib = rowB * b.Stride + x * 4;
        var pa = a.Pixels;
        var pb = b.Pixels;
        return Math.Abs(pa[ia] - pb[ib]) <= _o.Tolerance
               && Math.Abs(pa[ia + 1] - pb[ib + 1]) <= _o.Tolerance
               && Math.Abs(pa[ia + 2] - pb[ib + 2]) <= _o.Tolerance;
    }

    /// <summary>
    /// Rechte Randspalten, die trotz passender Verschiebung wechseln, sind ein Scrollbalken. Gewertet werden nur
    /// Zeilen, deren Hauptbereich ohne jede Ausnahme übereinstimmt – so stören feste Kopfzeilen und schwebende
    /// Elemente nicht, der wandernde Daumen des Scrollbalkens aber wird erkannt.
    /// </summary>
    private void TrackRightColumns(PixelFrame previous, PixelFrame current, byte[][] prevSignatures, byte[][] curSignatures, int dy)
    {
        var overlap = previous.Height - _top - _bottom - dy;
        if (overlap <= 0 || _columnChanges.Length == 0) return;
        var rows = new List<(int Previous, int Current)>();
        for (var i = 0; i < overlap; i++)
        {
            if (RowsMatch(prevSignatures[_top + i + dy], curSignatures[_top + i]))
                rows.Add((_top + i + dy, _top + i));
        }
        if (rows.Count < 8) return;
        var firstColumn = previous.Width - _columnChanges.Length;
        for (var c = 0; c < _columnChanges.Length; c++)
        {
            var x = firstColumn + c;
            var misses = 0;
            foreach (var (yPrevious, yCurrent) in rows)
                if (!SamePixel(previous, yPrevious, current, yCurrent, x)) misses++;
            if (misses > rows.Count * 0.01) _columnChanges[c]++;
        }
        _matchedFrames++;
    }
}
