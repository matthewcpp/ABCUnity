using System;
using System.IO;
using System.Collections.Generic;
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
        public Dictionary<GameObject, ABC.Item> itemMap { get; } = new Dictionary<GameObject, ABC.Item>();
        private Dictionary<int, List<SpriteRenderer>> spriteRendererCache = new Dictionary<int, List<SpriteRenderer>>();
        private TimeSignature timeSignature;


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

        public Alignment GetAlignment(int i)
        {
            return layouts[i].alignment;
        }

        void SetupVoiceLayouts()
        {
            // create the layout structures for each voice
            int measureCount = 0;
            for (int i = 0; i < tune.voices.Count; i++)
            {
                var layout = new VoiceLayout(tune.voices[i]);
                layout.Init();
                layouts.Add(layout);

                if (i == 0)
                {
                    measureCount = layout.alignment.measures.Count;
                }
                else
                {
                    if (layout.alignment.measures.Count != measureCount)
                        throw new LayoutException("All voices must have the same measure count");
                }
            }

            CreateSprites();
        }

        void CreateSprites()
        {
            foreach (var layout in layouts)
            {
                foreach (var measure in layout.alignment.measures)
                {
                    foreach (var beat in measure.beats)
                    {
                        foreach (var beatItem in beat.items)
                        {
                            switch (beatItem.item.type)
                            {
                                case ABC.Item.Type.Note:
                                    LayoutNote(beatItem.item as ABC.Note, layout, beatItem);
                                    break;

                                case ABC.Item.Type.Chord:
                                    LayoutChord(beatItem.item as ABC.Chord, layout, beatItem);
                                    break;

                                case ABC.Item.Type.Rest:
                                    LayoutRest(beatItem.item as ABC.Rest, layout, beatItem);
                                    break;

                                case ABC.Item.Type.MultiMeasureRest:
                                    LayoutMeasureRest(beatItem.item as ABC.MultiMeasureRest, layout, beatItem);
                                    break;
                            }

                            beat.minSize.x += beatItem.info.totalBounding.size.x + noteAdvance;
                            beat.minSize.y = Mathf.Max(beat.minSize.y, beatItem.info.totalBounding.size.y);

                            gameObjectMap[beatItem.item.id] = beatItem.container;
                            itemMap[beatItem.container] = beatItem.item;
                        }

                        measure.minSize.x += beat.minSize.x;
                        measure.minSize.y = Mathf.Max(measure.minSize.y, beat.minSize.y);
                    }

                    if (measure.bar != null)
                    {
                        measure.bar.container = new GameObject("Bar");
                        measure.bar.info = notes.CreateBar(measure.bar.item as ABC.Bar, measure.bar.container);
                        measure.minSize.x += measure.bar.info.totalBounding.size.x;
                        measure.minSize.y = Mathf.Max(measure.minSize.y, measure.bar.info.totalBounding.size.y);
                    }
                }
            }
        }

        void LayoutScoreLine(int lineNum)
        {
            foreach (var layout in layouts)
            {
                layout.NewStaffline();
                LayoutStaff(layout);
                if (lineNum == 0)
                    LayoutTimeSignature(layout);
            }

            for (int measure = 0; measure < layouts[0].scoreLines[lineNum].Count; measure++)
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
                        var measureInfo = layout.scoreLines[lineNum][measure];
                        var beatInfo = measureInfo.beats[layout.beatAlignmentIndex];

                        // if this beat is the start of a new group of notes render them
                        if (beatInfo.beatStart == beat)
                        {
                            foreach (var beatItem in beatInfo.items)
                                PositionItem(layout, beatItem);

                            if (layout.beatAlignmentIndex < measureInfo.beats.Count - 1)
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
                    var measureInfo = layout.scoreLines[lineNum][measure];
                    PositionBar(measureInfo.bar, layout);

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
        }

        void LayoutTune()
        {
            if (tune == null) return;
            
            notes = new NoteCreator(cache);
            cache.color = color;
            
            timeSignature = GetTimeSignature();

            var rect = rectTransform.rect;
            horizontalMax = rect.size.x * 1.0f / layoutScale;

            staffOffset = new Vector2(-rect.size.x / 2.0f, 0);

            Vector3 scale = this.gameObject.transform.localScale;
            this.gameObject.transform.localScale = Vector3.one;
            
            beams = Beam.CreateBeams(tune);
            SetupVoiceLayouts();

            for (int i = 0; i < layouts[0].scoreLines.Count; i++)
                LayoutScoreLine(i);

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
        
        void LayoutTimeSignature(VoiceLayout layout)
        {
            var timeSignature = layout.voice.items[0] as ABC.TimeSignature;
            if (timeSignature == null)
                throw new LayoutException($"expected voice: {layout.voice.name} to have Time Signature");

            var container = new GameObject("Time Signature");
            container.transform.parent = layout.staff.container.transform;
            var timeSignatureInfo = notes.CreateTimeSignature(timeSignature, container, layout.staff.position);

            layout.staff.UpdateBounds(timeSignatureInfo.totalBounding);
            layout.staff.position.x = timeSignatureInfo.totalBounding.max.x;
        }

        void AdjustStaffScale(VoiceLayout layout)
        {
            var currentWidth = layout.currentStaff.GetComponent<SpriteRenderer>().bounds.size.x;
            var scaleX = layout.staff.position.x / currentWidth;
            layout.currentStaff.transform.localScale = new Vector3(scaleX, 1.0f, 1.0f);
        }

        void PositionBar(Alignment.BeatItem bar, VoiceLayout layout)
        {
            bar.container.transform.parent = layout.measure.container.transform;
            bar.container.transform.localPosition = layout.measure.position;

            var totalBounding = new Bounds(bar.info.totalBounding.center + layout.measure.position, bar.info.totalBounding.size);
            layout.measure.UpdateBounds(totalBounding);
            layout.measure.position.x += totalBounding.size.x;
        }
        
        void PositionItem(VoiceLayout layout, Alignment.BeatItem beatItem)
        {
            beatItem.container.transform.parent = layout.measure.container.transform;
            beatItem.container.transform.localPosition = layout.measure.position;

            var totalBounding = new Bounds(beatItem.info.totalBounding.center + layout.measure.position, beatItem.info.totalBounding.size);
            var rootBounding = new Bounds(beatItem.info.rootBounding.center + layout.measure.position, beatItem.info.rootBounding.size);
            
            layout.measure.UpdateBounds(totalBounding);
            layout.measure.position.x += totalBounding.size.x + noteAdvance;

            var duration = beatItem.item as ABC.Duration;
            if (duration != null && beams.TryGetValue(duration.beam, out Beam beam))
                beam.Update(rootBounding, cache, layout);
        }

        void LayoutChord(ABC.Chord chordItem, VoiceLayout layout, Alignment.BeatItem beatItem)
        {
            beatItem.container = new GameObject("Chord");
            
            tune.decorations.TryGetValue(chordItem.id, out var decorations);
            
            NoteInfo chordInfo;
            if (beams.TryGetValue(chordItem.beam, out Beam beam))
                chordInfo = notes.CreateChord(chordItem, beam, decorations, beatItem.container);
            else
                chordInfo = notes.CreateChord(chordItem, layout.voice.clef, decorations, beatItem.container);
            
            beatItem.info = chordInfo;
        }

        void LayoutNote(ABC.Note noteItem, VoiceLayout layout, Alignment.BeatItem beatItem)
        {
            beatItem.container = new GameObject("Note");

            tune.decorations.TryGetValue(noteItem.id, out var decorations);

            NoteInfo noteInfo;
            if (beams.TryGetValue(noteItem.beam, out Beam beam))
                noteInfo = notes.CreateNote(noteItem, beam, decorations, beatItem.container);
            else
                noteInfo = notes.CreateNote(noteItem, layout.voice.clef, decorations, beatItem.container);
            
            beatItem.info = noteInfo;
        }

        void LayoutRest(ABC.Rest restItem, VoiceLayout layout, Alignment.BeatItem beatItem)
        {
            beatItem.container = new GameObject("Rest");

            var restInfo = notes.CreateRest(restItem, beatItem.container , layout.measure.position);
            beatItem.info = restInfo;
        }

        void LayoutMeasureRest(ABC.MultiMeasureRest measureRest, VoiceLayout layout, Alignment.BeatItem beatItem)
        {
            beatItem.container = new GameObject("Rest");

            var restInfo = notes.CreateMeasureRest(measureRest, beatItem.container, layout.measure.position);
            beatItem.info = restInfo;
        }
    }
}

