using ValleyFixes.Fixes;

namespace ValleyFixes.Tests
{
    public class SpriteFramesTests
    {
        private static readonly uint[] A = { 1, 2, 3 };
        private static readonly uint[] B = { 1, 9, 3 };

        [Fact]
        public void Copies_when_the_base_is_still_and_the_donor_moves()
        {
            Assert.True(SpriteFrames.ShouldCopyFromDonor(A, A, A, A, B, A));
        }

        [Fact]
        public void Does_nothing_when_the_base_frame_already_moves()
        {
            Assert.False(SpriteFrames.ShouldCopyFromDonor(A, B, A, A, B, A));
        }

        [Fact]
        public void Does_nothing_when_the_donor_does_not_move()
        {
            Assert.False(SpriteFrames.ShouldCopyFromDonor(A, A, A, A, A, A));
        }

        [Fact]
        public void Does_nothing_when_the_donor_disagrees_on_the_first_frame()
        {
            Assert.False(SpriteFrames.ShouldCopyFromDonor(A, A, A, B, B, A));
        }

        [Fact]
        public void Does_nothing_when_the_third_frames_differ()
        {
            Assert.False(SpriteFrames.ShouldCopyFromDonor(A, A, B, A, B, A));
        }

        [Fact]
        public void Different_sizes_are_never_the_same()
        {
            Assert.False(SpriteFrames.Same(new uint[] { 1 }, new uint[] { 1, 2 }));
        }
    }
}
