using UnityEngine;
using UnityEngine.EventSystems;

public class FocusDebugger : MonoBehaviour
{
    private void OnGUI()
    {
        if (EventSystem.current == null) return;

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        string text = selected != null ? $"Focus: {selected.name}" : "Focus: None";

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 24;
        style.normal.textColor = Color.yellow;
        style.alignment = TextAnchor.UpperRight;
        
        // Draw at top right
        GUI.Label(new Rect(Screen.width - 520, 20, 500, 50), text, style);
    }
}
