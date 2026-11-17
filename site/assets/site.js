// Marks draw themselves once when their section scrolls into view.
// Without JavaScript (or with reduced motion) every mark is simply visible; see style.css.
(() => {
  // Frames stretch with their text or image. A pixel-exact viewBox keeps the 4 px stroke exact.
  const frames = document.querySelectorAll(".mark-box > svg:first-of-type, .shot-mark > svg");
  const fit = (svg) => {
    const { width, height } = svg.getBoundingClientRect();
    if (!width || !height) return;
    svg.setAttribute("viewBox", `0 0 ${width.toFixed(1)} ${height.toFixed(1)}`);
    const rect = svg.querySelector("rect");
    rect.setAttribute("width", width.toFixed(1));
    rect.setAttribute("height", height.toFixed(1));
    svg.classList.add("is-sized");
    measure(rect);
  };
  // Stroke length for the draw-in (CSS reads --len); measured, because pathLength is unreliable on <rect>
  const measure = (shape) => shape.style.setProperty("--len", shape.getTotalLength().toFixed(1));
  document.querySelectorAll("[data-draw]").forEach(measure);
  frames.forEach(fit);
  if ("ResizeObserver" in window) {
    const resize = new ResizeObserver((entries) => entries.forEach((entry) => fit(entry.target)));
    frames.forEach((svg) => resize.observe(svg));
  }

  const groups = document.querySelectorAll("[data-marks]");
  if (!("IntersectionObserver" in window) || matchMedia("(prefers-reduced-motion: reduce)").matches) {
    groups.forEach((group) => group.classList.add("is-drawn"));
    return;
  }
  const observer = new IntersectionObserver(
    (entries) => {
      for (const entry of entries) {
        if (!entry.isIntersecting) continue;
        entry.target.classList.add("is-drawn");
        observer.unobserve(entry.target);
      }
    },
    { rootMargin: "0px 0px -12% 0px", threshold: 0.2 }
  );
  groups.forEach((group) => observer.observe(group));
})();
