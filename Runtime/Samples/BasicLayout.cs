using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;


namespace ABCUnity.Example
{
    public class BasicLayout : MonoBehaviour
    {
        [SerializeField] private TextMeshPro title;
        [SerializeField] private string resourceName;
        [SerializeField] private string abcString;
        [SerializeField] private string abcFile;

        [Range(0.0f, 1.0f)]public float layoutWidth = 0.8f;
        private Camera mainCamera;
        private Layout layout;
        private float aspect;
        private float orthographicSize;

        void Awake()
        {
            mainCamera = Camera.main;
            layout = FindObjectOfType<Layout>();
            
            ResizeLayout();
            layout.onLoaded += OnLoaded;
        }

        void OnLoaded(ABC.Tune tune)
        {
            title.text = tune.title;
        }

        void Start()
        {
            if (!string.IsNullOrEmpty(abcString)) {
                layout.LoadString(abcString);
                return;
            }

            if (!string.IsNullOrEmpty(abcFile)) {
                layout.LoadFile(abcFile);
                return;
            }

            if (!string.IsNullOrEmpty(resourceName)) {
                LoadFromResource(resourceName);
            }
        }

        void Update()
        {
            if (aspect != mainCamera.aspect || orthographicSize != mainCamera.orthographicSize)
                ResizeLayout();
        }

        public void LoadFromResource(string resourceName)
        {
            TextAsset abcTextAsset = Resources.Load(resourceName) as TextAsset;

            if (abcTextAsset)
                layout.LoadString(abcTextAsset.text);
        }

        private void ResizeLayout()
        {
            aspect = mainCamera.aspect;
            orthographicSize = mainCamera.orthographicSize;

            float orthoHeight = orthographicSize* 2.0f;
            float targetWidth = (orthoHeight * aspect) * layoutWidth;

            var titleTransform = title.rectTransform;
            titleTransform.position = new Vector3(0.0f, orthographicSize, 0.0f);
            titleTransform.sizeDelta = new Vector2 (targetWidth, titleTransform.sizeDelta.y);

            var layoutSpacer = 1.5f;
            var layoutTransform = layout.GetComponent<RectTransform>();
            layoutTransform.position = new Vector3(0.0f, titleTransform.position.y - titleTransform.rect.height - layoutSpacer, 0.0f);
            layoutTransform.sizeDelta = new Vector2(targetWidth, orthoHeight - titleTransform.sizeDelta.y - layoutSpacer);
        }
    }

}
