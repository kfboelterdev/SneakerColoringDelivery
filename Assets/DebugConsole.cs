using System.Collections;
using TMPro;
using UnityEngine;

public class DebugConsole : MonoSingleton<DebugConsole> {

    [SerializeField] private TextMeshProUGUI _debugConsole;

    public void PrintToConsole(string message) {
        _debugConsole.text += $">{message}\n";
    }

    public void Clear() {
        _debugConsole.text = "";
    }

}
