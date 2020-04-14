using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

namespace ABCUnity.Example
{
    public class LayoutSizer : MonoBehaviour
    {
        [SerializeField] private Camera camera;
        [SerializeField] private Layout layout;
        
        const float targetHeightScale = 0.80f;

        private float aspect;
        private float orthographicSize;

        public void Awake()
        {

            ResizeLayout();
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
            float targetHeight = orthoHeight * targetHeightScale;
            float targetWidth = orthoHeight * aspect;

            var layoutCollider = layout.GetComponent<BoxCollider2D>();
            layoutCollider.size = new Vector2(targetWidth, targetHeight);
            
            layout.transform.position = new Vector3(0.0f, (orthoHeight - targetHeight) / -2.0f, 0.0f);
        }
    }

}
