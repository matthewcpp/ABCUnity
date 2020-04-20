﻿using System;
using System.IO;
using System.Collections.Generic;
using ABC;
using UnityEngine;
using UnityEngine.U2D;

namespace ABCUnity
{
    public class Layout : MonoBehaviour
    {
        [SerializeField]
        private SpriteAtlas spriteAtlas; // set in editor

        [SerializeField]
        float layoutScale = 0.5f;
        
        [SerializeField]
        public Color color = Color.black;

        [SerializeField]
        public Material NoteMaterial;

        private SpriteCache cache;
        private NoteCreator notes;

        public ABC.Tune tune { get; private set; }
        private BoxCollider2D bounding;

        public delegate void OnLoaded(ABC.Tune tune);

        public OnLoaded onLoaded;

        public void Awake()
        {
            bounding = this.GetComponent<BoxCollider2D>();
            cache = new SpriteCache(spriteAtlas);
            notes = new NoteCreator(cache);
            NoteMaterial = GameObject.Instantiate(NoteMaterial);
            NoteMaterial.color = color;
        }

        public void LoadString(string abc)
        {
            try
            {
                tune = ABC.Tune.Load(abc);
                LayoutTune();
                onLoaded?.Invoke(tune);
            }
            catch (ABC.ParseException e)
            {
                Debug.Log(e.Message);
            }
        }

        public void LoadStream(Stream stream)
        {
            try
            {
                tune = ABC.Tune.Load(stream);
                LayoutTune();
                onLoaded?.Invoke(tune);
            }
            catch (ABC.ParseException e)
            {
                Debug.Log(e.Message);
            }
        }

        public Dictionary<ABC.Item, GameObject> gameObjectMap { get; } = new Dictionary<ABC.Item, GameObject>();
        public Dictionary<GameObject, ABC.Item> itemMap { get; } = new Dictionary<GameObject, Item>();

        public GameObject FindItemRootObject(GameObject obj)
        {
            while (!itemMap.ContainsKey(obj))
                obj = obj.transform.parent.gameObject;

            return obj;
        }

        public bool SetItemColor(ABC.Item item, Color color)
        {
            if (gameObjectMap.TryGetValue(item, out GameObject obj))
            {
                SetObjectColor(obj, color);
                return true;
            }

            return false;
        }

        private void SetObjectColor(GameObject target, Color noteColor)
        {
            var targetTransform = target.transform;

            for (int i = 0; i < targetTransform.childCount - 1; i++)
                targetTransform.GetChild(i).GetComponent<SpriteRenderer>().color = noteColor;

            var lastChild = targetTransform.GetChild(targetTransform.childCount - 1);
            if (lastChild.childCount  == 0)
                lastChild.GetComponent<SpriteRenderer>().color = noteColor;
        }
        
        const float staffPadding = 0.3f;
        const float measurePadding = 0.5f;
        const float staffMargin = 0.2f;
        const float clefAdvance = 2.0f;
        const float noteAdvance = 0.75f;

        Vector2 staffOffset;

        List<VoiceLayout> layouts = new List<VoiceLayout>();

        private float horizontalMax;

        void LayoutTune()
        {
            if (tune == null) return;
            cache.color = color;
            var timeSignature = GetTimeSignature();

            horizontalMax = bounding.size.x * 1.0f / layoutScale;

            staffOffset = new Vector2(-bounding.bounds.extents.x, bounding.bounds.extents.y);

            Vector3 scale = this.gameObject.transform.localScale;
            this.gameObject.transform.localScale = Vector3.one;

            // create the layout structures for each voice
            for (int i =0 ; i < tune.voices.Count; i++)
            {
                var layout = new VoiceLayout(tune.voices[i]);
                layouts.Add(layout);
                LayoutStaff(layout);
            }

            for (int measure = 0; measure < layouts[0].alignment.measures.Count; measure++)
            {
                foreach (var layout in layouts)
                {
                    layout.NewMeasure();
                    layout.measure.position.x = measurePadding;
                }
                    

                for (int beat = 1; beat <= timeSignature.beatCount; beat++)
                {
                    float maxBeatX = float.MinValue;

                    foreach (var layout in layouts)
                    {
                        var measureInfo = layout.alignment.measures[measure];
                        var beatInfo = measureInfo.beatItems[layout.beatAlignmentIndex];

                        // if this beat is the start of a new group of notes render them
                        if (beatInfo.beatStart == beat)
                        {
                            foreach (var item in beatInfo.items)
                            {
                                switch (item.type)
                                {
                                    case ABC.Item.Type.Note:
                                        LayoutNote(item as ABC.Note, layout);
                                        break;

                                    case ABC.Item.Type.Chord:
                                        LayoutChord(item as ABC.Chord, layout);
                                        break;

                                    case ABC.Item.Type.Rest:
                                        LayoutRest(item as ABC.Rest, layout);
                                        break;
                                }
                            }

                            if (layout.beatAlignmentIndex < measureInfo.beatItems.Count - 1)
                                layout.beatAlignmentIndex += 1;
                        }

                        maxBeatX = Math.Max(maxBeatX, layout.measure.position.x);
                    }

                    // in order to preserve alignment, all layouts will advance to the furthest position of the current beat marker
                    foreach (var layout in layouts)
                        layout.measure.position.x = maxBeatX;
                }

                bool newLineNeeded = false;

                // render the bar to end the measure and ensure they will all fit on new staff line
                foreach (var layout in layouts)
                {
                    var measureInfo = layout.alignment.measures[measure];
                    LayoutBar(measureInfo.bar, layout);

                    if (layout.staff.position.x + layout.measure.position.x > horizontalMax)
                        newLineNeeded = true;
                }

                if (newLineNeeded)
                {
                    FinalizeStaffLines();

                    foreach (var layout in layouts)
                    {
                        layout.NewStaffline();
                        LayoutStaff(layout);
                    }
                }

                // Add the measure to the staff line
                foreach (var layout in layouts)
                {
                    if (layout.measureVertices.Count > 0)
                    {
                        var mesh = new Mesh();
                        mesh.vertices = layout.measureVertices.ToArray();

                        var triangles = new List<int>();
                        for (int i = 0; i < layout.measureVertices.Count; i += 4)
                        {
                            triangles.Add(i);
                            triangles.Add(i + 1);
                            triangles.Add(i + 2);
                            triangles.Add(i + 2);
                            triangles.Add(i + 1);
                            triangles.Add(i + 3);
                        }

                        mesh.triangles = triangles.ToArray();

                        var item = new GameObject();
                        var meshRenderer = item.AddComponent<MeshRenderer>();
                        meshRenderer.sharedMaterial = NoteMaterial;

                        var meshFilter = item.AddComponent<MeshFilter>();
                        meshFilter.mesh = mesh;

                        item.transform.parent = layout.measure.container.transform;
                    }

                    layout.measure.container.transform.localPosition = layout.staff.position;
                    layout.measure.container.transform.parent = layout.staff.container.transform;
                    layout.staff.position.x += layout.measure.position.x;
                    layout.UpdateStaffBounding();
                }
            }

            FinalizeStaffLines();

            this.gameObject.transform.localScale = scale;
        }

        TimeSignature GetTimeSignature()
        {
            TimeSignature result = null;

            for (int i = 0; i < tune.voices.Count; i++)
            {
                var timeSignatureItem = tune.voices[i].items[0] as ABC.TimeSignature;
                if (timeSignatureItem == null)
                    throw new BeatAlignmentException($"Voice {i} does not initially declare a time signature.");

                var timeSignature = TimeSignature.Parse(timeSignatureItem.value);

                if (result == null)
                    result = timeSignature;
                else if (!timeSignature.Equals(result))
                    throw new LayoutException("All voices should have the same time signature");

            }

            return result;
        }

        /// <summary>Calculates the final size of the staff and positions it correctly relative to the container.</summary>
        void FinalizeStaffLines()
        {
            foreach (var layout in layouts)
            {
                AdjustStaffScale(layout);

                layout.staff.container.transform.parent = this.transform;
                layout.staff.container.transform.localPosition = new Vector3(staffOffset.x, staffOffset.y - (layout.staff.maxY * layoutScale), 0.0f);
                layout.staff.container.transform.localScale = new Vector3(layoutScale, layoutScale, layoutScale);

                staffOffset.y -= (layout.staff.height + staffMargin) * layoutScale;
            }
        }

        void LayoutStaff(VoiceLayout layout)
        {
            var staff = cache.GetSpriteObject("Staff");
            layout.currentStaff = staff.gameObject;
            layout.currentStaff.transform.parent = layout.staff.container.transform;
            layout.currentStaff.transform.localPosition = layout.staff.position;

            layout.staff.UpdateBounds(staff.bounds);
            layout.staff.position.x += staffPadding;

            var clef = cache.GetSpriteObject($"Clef_{layout.voice.clef.ToString()}");
            clef.transform.parent = layout.staff.container.transform;
            clef.transform.localPosition = layout.staff.position;

            layout.staff.UpdateBounds(clef.bounds);
            layout.staff.position.x += clefAdvance;
        }

        void AdjustStaffScale(VoiceLayout layout)
        {
            var currentWidth = layout.currentStaff.GetComponent<SpriteRenderer>().bounds.size.x;
            var scaleX = layout.staff.position.x / currentWidth;
            layout.currentStaff.transform.localScale = new Vector3(scaleX, 1.0f, 1.0f);
        }

        void LayoutBar(ABC.Bar barItem, VoiceLayout layout)
        {
            var barObj = cache.GetSpriteObject("Bar_Line");
            barObj.transform.parent = layout.measure.container.transform;
            barObj.transform.localPosition = layout.measure.position;
        }

        void LayoutChord(ABC.Chord chordItem, VoiceLayout layout)
        {
            var container = new GameObject("Chord");
            container.transform.parent = layout.measure.container.transform;
            
            gameObjectMap[chordItem] = container;
            itemMap[container] = chordItem;
            
            var chord = notes.CreateChord(chordItem, layout.voice.clef, container, layout.measure.position);

            Bounds chordBounds = chord[0].bounds;

            for (int i = 1; i < chord.Count; i++)
            {
                layout.measure.UpdateBounds(chord[i].bounds);
                chordBounds.Encapsulate(chord[i].bounds);
            }

            layout.measure.position.x = chordBounds.max.x + noteAdvance;
        }
        
        void LayoutNote(ABC.Note noteItem, VoiceLayout layout)
        {
            var container = new GameObject("Note");
            container.transform.parent = layout.measure.container.transform;
            
            gameObjectMap[noteItem] = container;
            itemMap[container] = noteItem;

            SpriteRenderer note = null;
            if (layout.alignment.beams.TryGetValue(noteItem.beam, out Beam beam))
            {
                note = notes.CreateNote(noteItem, beam, container, layout.measure.position);
                beam.Update(note, cache, layout);
            }
            else
            {
                note = notes.CreateNote(noteItem, layout.voice.clef, container, layout.measure.position);
            }
            
            layout.measure.UpdateBounds(note.bounds);
            layout.measure.position.x = note.bounds.max.x + noteAdvance;
        }

        void LayoutRest(ABC.Rest restItem, VoiceLayout layout)
        {
            var container = new GameObject("Rest");
            container.transform.parent = layout.measure.container.transform;

            gameObjectMap[restItem] = container;
            itemMap[container] = restItem;
            
            var rest = notes.CreateRest(restItem, container, layout.measure.position);
            layout.measure.UpdateBounds(rest.bounds);
            layout.measure.position.x = rest.bounds.max.x + noteAdvance;
        }
    }
}

