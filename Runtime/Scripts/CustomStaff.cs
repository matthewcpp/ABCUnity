using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;
using TMPro;

namespace ABCUnity
{
    public class CustomStaff
    {
        SpriteCache cache;
        NoteCreator notes;

        public Dictionary<int, List<string>> decorations { get; } = new Dictionary<int, List<string>>();

        ABC.Clef clef;
        public GameObject container { get; }

        public bool isInit { get; private set; }
        public float staffWidth { get { return currentWidth; } set { SetWidth(value);  } }
        float staffSpriteBaseWidth = 0.0f;
        float currentWidth = 0.0f;

        public CustomStaff(GameObject parent, SpriteAtlas spriteAtlas, TextMeshPro textPrefab)
        {
            var staffContainer = new GameObject("Custom Staff");
            staffContainer.transform.parent = parent.transform;
            staffContainer.transform.localPosition = Vector3.zero;
            this.container = staffContainer;

            cache = new SpriteCache(spriteAtlas, textPrefab);
            notes = new NoteCreator(cache);
        }

        public void Init(ABC.Clef clef)
        {
            this.clef = clef;
            var info = notes.CreateStaff(this.clef, container, Vector3.zero);

            SetWidth(info.totalBounding.size.x);
            isInit = true;
        }

        private void SetWidth(float newWidth)
        {
            var staff = container.transform.GetChild(0).gameObject;
            var staffRenderer = staff.GetComponent<SpriteRenderer>();

            if (staffSpriteBaseWidth == 0.0f)
                staffSpriteBaseWidth = staffRenderer.bounds.size.x;

            var scaleX = newWidth / staffSpriteBaseWidth;
            staffRenderer.transform.localScale = new Vector3(scaleX, 1.0f, 1.0f);
            this.currentWidth = newWidth;
        }

        public GameObject AppendNote(ABC.Note note, float pos)
        {
            return AppendNote(note, null, pos);
        }

        public GameObject AppendNote(ABC.Note note, List<string> decorations, float pos)
        {
            GameObject noteContainer = new GameObject();
            noteContainer.transform.parent = container.transform;

            notes.CreateNote(note, this.clef, decorations, noteContainer);
            noteContainer.transform.localPosition = new Vector3(pos, 0.0f, 0.0f);
            noteContainer.transform.localScale = Vector3.one; // clear any scaling set on the parent

            return noteContainer;
        }
    }

}

