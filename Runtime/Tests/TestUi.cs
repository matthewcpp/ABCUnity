using System.IO;
using System.Collections.Generic;
    
using UnityEngine;
using TMPro;
using UnityEngine.UIElements;

public class TestUi : MonoBehaviour
{
    private TMP_Dropdown testSelector;
    private ABCUnity.Layout layout;

    /// <summary>
    /// Specifies a file path which will override any dropdown selection when the "Load" Button is clicked.
    /// This is primarily used for iteratively building up tests.
    /// </summary>
    [SerializeField] string filePathOverride = null;

    void Awake()
    {
        testSelector = FindObjectOfType<TMP_Dropdown>();
        layout = FindObjectOfType<ABCUnity.Layout>();

        PopulateDropdown();
    }

    private static string[] testFiles = new string[] {
        "Alignment.abc",
        "Bars.abc",
        "Beams.abc",
        "Chords.abc",
        "Notes.abc",
        "Rests.abc",
        "Slurs.abc"
    };

    void PopulateDropdown()
    {
        var options = new List<TMP_Dropdown.OptionData>();

        foreach (var testFile in testFiles)
            options.Add(new TMP_Dropdown.OptionData(Path.GetFileName(testFile)));

        testSelector.options = options;
    }

    public void LoadSelectedTest()
    {
        if (!string.IsNullOrEmpty(filePathOverride))
        {
            Debug.Log($"Load File from Override: {filePathOverride}");
            layout.LoadFile(filePathOverride);
        }
        else
        {
            var testName = testSelector.options[testSelector.value].text;
            var resourceName = $"Tests/{testName}";

            TextAsset abcTextAsset = Resources.Load(resourceName) as TextAsset;

            if (abcTextAsset)
            {
                Debug.Log($"Load Test: {resourceName}");
                layout.LoadString(abcTextAsset.text);
            }
            else
            {
                Debug.Log($"Failed to load resource: {resourceName}");
            }
        }
    }
}
