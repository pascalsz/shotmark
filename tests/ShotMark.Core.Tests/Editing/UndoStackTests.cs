using ShotMark.Core.Editing;
namespace ShotMark.Core.Tests.Editing;

public class UndoStackTests
{
    [Fact]
    public void Undo_and_redo_walk_through_states()
    {
        var stack = new UndoStack<string>();
        stack.Record("A");          // A -> B
        stack.Record("B");          // B -> C
        Assert.Equal("B", stack.Undo("C"));
        Assert.Equal("A", stack.Undo("B"));
        Assert.Null(stack.Undo("A"));
        Assert.Equal("B", stack.Redo("A"));
        Assert.True(stack.CanRedo);
    }

    [Fact]
    public void Recording_clears_redo()
    {
        var stack = new UndoStack<string>();
        stack.Record("A");
        stack.Undo("B");
        stack.Record("A");
        Assert.False(stack.CanRedo);
    }

    [Fact]
    public void Limit_drops_oldest()
    {
        var stack = new UndoStack<string>(limit: 2);
        stack.Record("1"); stack.Record("2"); stack.Record("3");
        Assert.Equal("3", stack.Undo("4"));
        Assert.Equal("2", stack.Undo("3"));
        Assert.False(stack.CanUndo);
    }
}
