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
        
        // Draw at top left
        GUI.Label(new Rect(20, 20, 500, 50), text, style);
    }
}
