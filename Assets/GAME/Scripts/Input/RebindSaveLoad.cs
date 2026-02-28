using UnityEngine;
using UnityEngine.InputSystem;

public class RebindSaveLoad : MonoBehaviour
{
    const string Key = "rebinds_json";

    public void Save()
    {
        var actions = GameInputInstaller.Instance.Actions;
        var json = actions.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString(Key, json);
        PlayerPrefs.Save();
    }

    public void Load()
    {
        var actions = GameInputInstaller.Instance.Actions;
        var json = PlayerPrefs.GetString(Key, "");
        if (!string.IsNullOrEmpty(json))
            actions.LoadBindingOverridesFromJson(json);
    }

    public void ResetAll()
    {
        var actions = GameInputInstaller.Instance.Actions;
        actions.RemoveAllBindingOverrides();
        PlayerPrefs.DeleteKey(Key);
    }
}
