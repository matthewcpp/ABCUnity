using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ABCUnity.Example
{
    public class LoadFromResources : MonoBehaviour
    {
        [SerializeField]
        string resourceName = "sample.abc";

        [SerializeField]
        Layout layout;

        void Start()
        {
            TextAsset abcTextAsset = Resources.Load(resourceName) as TextAsset;

            if (abcTextAsset)
            {
                layout.LoadString(abcTextAsset.text);
            }

            Destroy(this.gameObject);
        }
    }

}

