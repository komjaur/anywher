// RecipeCard.cs – tiny helper component living on the prefab
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class RecipeCard : MonoBehaviour
{
    public Image            background;
    public Image            icon;
    public TextMeshProUGUI  label;
    public Button           button;

    static readonly Color OK   = new (0.24f, 0.24f, 0.24f);
    static readonly Color BAD  = new (0.12f, 0.12f, 0.12f);

    public void SetState (bool craftable)
    {
        background.color = craftable ? OK : BAD;
        button.interactable = craftable;
    }
}
