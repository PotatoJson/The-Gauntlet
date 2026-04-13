using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Settings;
using TMPro;

public class LocaleSelector : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown localeDropdown;
    private bool _active = false;

    private void Start()
    {
        // Wait for the localization system to initialize before populating
        StartCoroutine(InitializeLocaleDropdown());
    }

    private IEnumerator InitializeLocaleDropdown()
    {
        yield return LocalizationSettings.InitializationOperation;

        // Populate dropdown with available locales from your project
        var options = new List<TMP_Dropdown.OptionData>();
        int selected = 0;

        for (int i = 0; i < LocalizationSettings.AvailableLocales.Locales.Count; i++)
        {
            var locale = LocalizationSettings.AvailableLocales.Locales[i];

            // This uses the "Native Name" (e.g., "English")
            options.Add(new TMP_Dropdown.OptionData(locale.LocaleName));

            if (LocalizationSettings.SelectedLocale == locale)
                selected = i;
        }

        localeDropdown.options = options;
        localeDropdown.value = selected;
        localeDropdown.onValueChanged.AddListener(ChangeLocale);

        _active = true;
    }

    public void ChangeLocale(int index)
    {
        if (!_active) return;
        StartCoroutine(SetLocale(index));
    }

    private IEnumerator SetLocale(int index)
    {
        _active = false;
        yield return LocalizationSettings.InitializationOperation;

        // This is the core line that switches the language
        LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[index];
        _active = true;
    }
}