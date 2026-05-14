using DustAdvisor.Ui.Export;
using FluentAssertions;
using Xunit;

namespace DustAdvisor.Ui.Tests
{
    public class UndoStackTests
    {
        [Fact]
        public void Push_then_Undo_invokes_inverse_action()
        {
            var stack = new UndoStack(maxDepth: 10);
            int counter = 0;
            stack.Push("inc", () => counter++);
            stack.Undo();
            counter.Should().Be(1);
        }

        [Fact]
        public void Undo_then_Redo_invokes_original_action()
        {
            var stack = new UndoStack(maxDepth: 10);
            int redoCount = 0;
            int undoCount = 0;
            stack.Push("test", undo: () => undoCount++, redo: () => redoCount++);
            stack.Undo();
            stack.Redo();
            undoCount.Should().Be(1);
            redoCount.Should().Be(1);
        }

        [Fact]
        public void MaxDepth_drops_oldest_when_exceeded()
        {
            var stack = new UndoStack(maxDepth: 2);
            int a = 0, b = 0, c = 0;
            stack.Push("a", () => a++);
            stack.Push("b", () => b++);
            stack.Push("c", () => c++);
            stack.Undo(); // should undo "c"
            stack.Undo(); // should undo "b"
            stack.Undo(); // "a" was dropped; should be a no-op
            a.Should().Be(0);
            b.Should().Be(1);
            c.Should().Be(1);
        }

        [Fact]
        public void Undo_returns_label_of_action_undone()
        {
            var stack = new UndoStack(maxDepth: 10);
            stack.Push("disenchant Yogg", () => { });
            stack.Undo().Should().Be("disenchant Yogg");
        }

        [Fact]
        public void Undo_returns_null_when_stack_empty()
        {
            var stack = new UndoStack(maxDepth: 10);
            stack.Undo().Should().BeNull();
        }
    }
}
