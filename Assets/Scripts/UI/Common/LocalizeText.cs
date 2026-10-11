using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Seunghak.Common;

public class LocalizeText : MonoBehaviour
{
    [SerializeField] private string convertStringEnum;
    [SerializeField] private TextMeshProUGUI currentText;

    void Start()
    {
        if(currentText == null)
        {
            Destroy(this);
            return;
        }

        SetStringKey(convertStringEnum);
    }
    public void SetStringKey(string stringEnum)
    {
        string inputtext = "";

        inputtext = StringManager.GetString(stringEnum);

        if (inputtext == null)
        {
            currentText.text = stringEnum;
        }
        else
        {
            currentText.text = inputtext;
        }
    }
    public void SetString(string stringEnum, params string[] inputdatas)
    {
        string inputtext = null;

        inputtext = StringManager.GetString(stringEnum);

        if (inputtext == null)
        {
            inputtext = string.Format(inputtext, inputdatas);
        }
        else
        {
            inputtext = stringEnum;
        }

        currentText.text = inputtext;
    }
}
