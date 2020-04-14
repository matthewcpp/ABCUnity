using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ABCUnity.Example
{
    public class SampleUI : MonoBehaviour
    {
        [SerializeField] private Layout layout;
        [SerializeField] private Text title;

        private void Awake()
        {
            layout.onLoaded += OnTuneLoaded;
        }

        private void OnTuneLoaded(ABC.Tune tune)
        {
            title.text = tune.title;
        }
    }
}

