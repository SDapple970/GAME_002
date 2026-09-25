namespace Game.NonCombat.Save
{
    public interface ISaveDataProvider
    {
        void CaptureSaveData(GameSaveData saveData);
    }

    public interface ISaveDataConsumer
    {
        void RestoreSaveData(GameSaveData saveData);
    }

    public interface INewGameRuntimeReset
    {
        void ResetForNewGame();
    }
}
