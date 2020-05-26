using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;


namespace ABCUnity.Example
{
    public class LayoutSizer : MonoBehaviour
    {
        [SerializeField] private Camera camera;
        [SerializeField] private Layout layout;
        [SerializeField] private TextMeshPro title;
        
        private float aspect;
        private float orthographicSize;

        public void Awake()
        {
            ResizeLayout();
            layout.onLoaded += OnLoaded;
        }

        void OnLoaded(ABC.Tune tune)
        {
            title.text = tune.title;
        }

        public void Update()
        {
            if (aspect != camera.aspect || orthographicSize != camera.orthographicSize)
                ResizeLayout();
        }

        private void ResizeLayout()
        {
            aspect = camera.aspect;
            orthographicSize = camera.orthographicSize;

            float orthoHeight = orthographicSize* 2.0f;
            float targetWidth = (orthoHeight * aspect) * 0.8f;

            var titleTransform = title.rectTransform;
            titleTransform.position = new Vector3(0.0f, orthographicSize, 0.0f);
            titleTransform.sizeDelta = new Vector2 (targetWidth, titleTransform.sizeDelta.y);

            var layoutTransform = layout.GetComponent<RectTransform>();
            layoutTransform.position = new Vector3(0.0f, titleTransform.position.y - titleTransform.rect.height, 0.0f);
            layoutTransform.sizeDelta = new Vector2(targetWidth, orthoHeight - titleTransform.sizeDelta.y);
        }
    }

}
