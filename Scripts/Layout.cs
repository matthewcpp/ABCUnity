using System;
using System.IO;
using System.Collections.Generic;
using ABC;
using TMPro;
using UnityEngine;
using UnityEngine.U2D;

namespace ABCUnity
{
    public class Layout : MonoBehaviour
    {
        [SerializeField]
        private SpriteAtlas spriteAtlas; // set in editor

        [SerializeField]
        public float layoutScale = 0.5f;
        
        [SerializeField]
        public Color color = Color.black;

        [SerializeField]
        public Material NoteMaterial;

        [SerializeField] public TextMeshPro textPrefab;

        private SpriteCache cache;
        private NoteCreator notes;

        public ABC.Tune tune { get; private set; }

        public delegate void OnLoaded(ABC.Tune tune);

        public OnLoaded onLoaded;

        public RectTransform rectTransform { get; private set; }

        public void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            cache = new SpriteCache(spriteAtlas, textPrefab);

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

        public void LoadFile(string path)
        {
            using (FileStream file = File.OpenRead(path))
            {
                LoadStream(file);
            }
        }

        public Dictionary<int, GameObject> gameObjectMap { get; } = new Dictionary<int, GameObject>();
        public Dictionary<GameObject, ABC.Item> itemMap { get; } = new Dictionary<GameObject, Item>();
        private Dictionary<int, List<SpriteRenderer>> spriteRendererCache = new Dictionary<int, List<SpriteRenderer>>();


        public GameObject FindItemRootObject(GameObject obj)
        {
            while (!itemMap.ContainsKey(obj))
                obj = obj.transform.parent.gameObject;

            return obj;
        }

        public bool SetItemColor(ABC.Item item, Color color)
        {
            if (gameObjectMap.TryGetValue(item.id, out GameObject obj))
            {
                if (!spriteRendererCache.TryGetValue(item.id, out var spriteRenderers))
                {
                    spriteRenderers = Util.GatherSpriteRenderers(obj);
                    spriteRendererCache[item.id] = spriteRenderers;
                }

                foreach (var spriteRenderer in spriteRenderers)
                    spriteRenderer.color = color;
                
                return true;
            }

            return false;
        }

        public void ResetItemColors()
        {
            foreach (var spriteRenderes in spriteRendererCache.Values)
            {
                foreach (var spriteRenderer in spriteRenderes)
                    spriteRenderer.color = color;
            }
        }

        public const float staffPadding = 0.3f;
        const float measurePadding = 0.5f;
        const float staffMargin = 0.2f;
        public const float noteAdvance = 0.75f;

        Vector2 staffOffset;

        List<VoiceLayout> layouts = new List<VoiceLayout>();
        Dictionary<int, Beam> beams;

        private float horizontalMax;

        public BeatAlignment GetAlignment(int i)
        {
            return layouts[i].alignment;
        }

        void LayoutTune()
        {
            if (tune == null) return;
            
            notes = new NoteCreator(cache);
            cache.color = color;
            
            var timeSignature = GetTimeSignature();

            var rect = rectTransform.rect;
            horizontalMax = rect.size.x * 1.0f / layoutScale;

            staffOffset = new Vector2(-rect.size.x / 2.0f, 0);

            Vector3 scale = this.gameObject.transform.localScale;
            this.gameObject.transform.localScale = Vector3.one;

            // create the layout structures for each voice
            for (int i =0 ; i < tune.voices.Count; i++)
            {
                var layout = new VoiceLayout(tune.voices[i]);
                layout.CreateAlignmentMap(beams);

                layouts.Add(layout);
                LayoutStaff(layout);
                LayoutTimeSignature(layout);
            }

            beams = Beam.CreateBeams(tune);

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

                                    case ABC.Item.Type.MultiMeasureRest:
                                        LayoutMeasureRest(item as ABC.MultiMeasureRest, layout);
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

                    // TODO: This calculation probably needs adjusting to be totally correct.
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
                    CreateMeshForMeasure(layout);

                    float measureAdjustment = calculateMeasureAdjustment();

                    layout.measure.container.transform.localPosition = layout.staff.position + new Vector3(measureAdjustment, 0.0f, 0.0f);
                    layout.measure.container.transform.parent = layout.staff.container.transform;
                    layout.staff.position.x += layout.measure.position.x + measureAdjustment;
                    layout.UpdateStaffBounding();
                }
            }

            FinalizeStaffLines();

            this.gameObject.transform.localScale = scale;
        }

        /// <summary>
        /// If a measure has a negative min value we need to move it over so that it will fit in the measure correctly.
        /// Note: This will most likely happen when the first note in a measure has an accidental attached to it.
        /// </summary>
        float calculateMeasureAdjustment()
        {
            float minX = 0.0f;

            foreach (var layout in layouts)
                minX = Mathf.Min(minX, layout.measure.bounds.min.x);

            return Mathf.Abs(minX) + measurePadding;
        }

        void CreateMeshForMeasure(VoiceLayout layout)
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
                layout.staff.container.transform.localPosition = new Vector3(staffOffset.x, staffOffset.y - (layout.staff.bounds.max.y * layoutScale), 0.0f);
                layout.staff.container.transform.localScale = new Vector3(layoutScale, layoutScale, layoutScale);

                staffOffset.y -= (layout.staff.bounds.size.y + staffMargin) * layoutScale;
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
            layout.staff.position.x = clef.bounds.max.x + noteAdvance;
        }

        const float TimeSignatureY = 1.15f;

        void LayoutTimeSignature(VoiceLayout layout)
        {
            var timeSignature = layout.voice.items[0] as ABC.TimeSignature;
            if (timeSignature == null)
                throw new LayoutException($"expected voice: {layout.voice.name} to have Time Signature");

            if (timeSignature.value == "C" || timeSignature.value == "C|")
            {
                var spriteName = (timeSignature.value == "C") ? "Time_Common" : "Time_Cut";
                var offset = layout.staff.position + new Vector3(0.0f, TimeSignatureY, 0.0f);
                var commonTime = cache.GetSpriteObject(spriteName);
                commonTime.transform.parent = layout.staff.container.transform;
                commonTime.transform.position = offset;

                layout.staff.UpdateBounds(commonTime.bounds);
                layout.staff.position.x = commonTime.bounds.max.x;
            }
            else
            {
                var pieces = timeSignature.value.Split('/');
                if (pieces.Length < 2)
                    throw new LayoutException($"Unable to parse time signature: {timeSignature.value}");

                var sprite = cache.GetSpriteObject($"Time_{pieces[0]}");
                sprite.transform.parent = layout.staff.container.transform;
                sprite.transform.position = layout.staff.position + new Vector3(0.0f, TimeSignatureY, 0.0f);
                layout.staff.UpdateBounds(sprite.bounds);

                sprite = cache.GetSpriteObject($"Time_{pieces[1]}");
                sprite.transform.parent = layout.staff.container.transform;
                sprite.transform.position = layout.staff.position;
                layout.staff.UpdateBounds(sprite.bounds);
                layout.staff.position.x = sprite.bounds.max.x;
            }

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
            tune.decorations.TryGetValue(chordItem.id, out var decorations);
            var container = new GameObject("Chord");
            container.transform.parent = layout.measure.container.transform;
            
            gameObjectMap[chordItem.id] = container;
            itemMap[container] = chordItem;

            var chordInfo = new NoteCreator.NoteInfo();
            if (beams.TryGetValue(chordItem.beam, out Beam beam))
            {
                
                chordInfo = notes.CreateChord(chordItem, beam, decorations, container, layout.measure.position);
                beam.Update(chordInfo.rootBounding, cache, layout);
            }
            else
            {
                chordInfo = notes.CreateChord(chordItem, layout.voice.clef, decorations, container, layout.measure.position);
            }

            layout.measure.UpdateBounds(chordInfo.totalBounding);
            layout.measure.position.x = chordInfo.totalBounding.max.x + noteAdvance;
        }
        
        void LayoutNote(ABC.Note noteItem, VoiceLayout layout)
        {
            tune.decorations.TryGetValue(noteItem.id, out var decorations);
            var container = new GameObject("Note");
            container.transform.parent = layout.measure.container.transform;
            
            gameObjectMap[noteItem.id] = container;
            itemMap[container] = noteItem;

            var layoutItem = new NoteCreator.NoteInfo();
            if (beams.TryGetValue(noteItem.beam, out Beam beam))
            {
                layoutItem = notes.CreateNote(noteItem, beam, decorations, container, layout.measure.position);
                beam.Update(layoutItem.rootBounding, cache, layout);
            }
            else
            {
                layoutItem = notes.CreateNote(noteItem, layout.voice.clef, decorations, container, layout.measure.position);
            }
            
            layout.measure.UpdateBounds(layoutItem.totalBounding);
            layout.measure.position.x = layoutItem.totalBounding.max.x + noteAdvance;
        }

        void LayoutRest(ABC.Rest restItem, VoiceLayout layout)
        {
            var container = new GameObject("Rest");
            container.transform.parent = layout.measure.container.transform;

            gameObjectMap[restItem.id] = container;
            itemMap[container] = restItem;
            
            var rest = notes.CreateRest(restItem, container, layout.measure.position);
            layout.measure.UpdateBounds(rest.bounds);
            layout.measure.position.x = rest.bounds.max.x + noteAdvance;
        }

        void LayoutMeasureRest(ABC.MultiMeasureRest measureRest, VoiceLayout layout)
        {
            var container = new GameObject("Rest");
            container.transform.parent = layout.measure.container.transform;

            gameObjectMap[measureRest.id] = container;
            itemMap[container] = measureRest;

            var rest = notes.CreateMeasureRest(measureRest, container, layout.measure.position);
            layout.measure.UpdateBounds(rest.bounds);
            layout.measure.position.x = rest.bounds.max.x + noteAdvance;
        }
    }
}

