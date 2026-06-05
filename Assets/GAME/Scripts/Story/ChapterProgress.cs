using System;

namespace Game.Story
{
    [Serializable]
    public sealed class ChapterProgress
    {
        public ChapterId chapterId = ChapterId.None;
        public int step;
        public bool completed;

        public ChapterProgress()
        {
        }

        public ChapterProgress(ChapterId chapterId)
        {
            this.chapterId = chapterId;
            step = 0;
            completed = false;
        }
    }
}
