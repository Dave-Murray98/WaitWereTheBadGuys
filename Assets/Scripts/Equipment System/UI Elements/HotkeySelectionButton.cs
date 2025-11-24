using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HotkeySelectionButton : MonoBehaviour
{
    public Image itemIcon;

    public TextMeshProUGUI itemNameText;
    public TextMeshProUGUI slotNumberText;
    public TextMeshProUGUI stackIndicator;

    public Button button;

    public void SetupButton(HotkeySelectionUI hotkeySelectionUI, int slotNumber)
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button != null)
        {
            button.onClick.AddListener(() => hotkeySelectionUI.OnSlotSelected(slotNumber));
            //Debug.Log("HotkeySelectionButton: Button set up for slot " + slotNumber);
        }
    }


}
