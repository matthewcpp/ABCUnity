using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ABCUnity
{
    public static class Util
    {
        public static void SetObjectColor(GameObject target, Color noteColor)
        {
            var targetTransform = target.transform;

            for (int i = 0; i < targetTransform.childCount - 1; i++)
                targetTransform.GetChild(i).GetComponent<SpriteRenderer>().color = noteColor;

            var lastChild = targetTransform.GetChild(targetTransform.childCount - 1);
            if (lastChild.childCount == 0)
                lastChild.GetComponent<SpriteRenderer>().color = noteColor;
        }

        public static List<SpriteRenderer> GatherSpriteRenderers(GameObject item)
        {
            var itemTransform = item.transform;
            var spriteRenderers = new List<SpriteRenderer>();

            for (int i = 0; i < itemTransform.childCount - 1; i++)
                spriteRenderers.Add(itemTransform.GetChild(i).GetComponent<SpriteRenderer>());


            var lastChild = itemTransform.GetChild(itemTransform.childCount - 1);
            if (lastChild.childCount > 0)
            {
                foreach (Transform child in lastChild)
                    spriteRenderers.Add(child.GetComponent<SpriteRenderer>());
            }
            else
            {
                spriteRenderers.Add(lastChild.GetComponent<SpriteRenderer>());
            }

            return spriteRenderers;
        }
    }
}