using System;
using System.IO;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.SocialPlatforms.Impl;

namespace ABCUnity
{
    public class Layout : MonoBehaviour
    {
        [SerializeField] private SpriteAtlas spriteAtlas; // set in editor
        [SerializeField] public Color color = Color.black;
        [SerializeField] public Material NoteMaterial;
        [SerializeField] public TextMeshPro textPrefab;
        [SerializeField] public float staffLinePadding = 0.4f;
        [SerializeField] public float staffLineMargin = 1.0f;

        [SerializeField] public bool overrideLineBreaks = false;

        private SpriteCache cache;
        private NoteCreator notes;

        private bool multilineLayout;

        public ABC.Tune tune { get; private set; }

        public delegate void OnLoaded(ABC.Tune tune);

        public OnLoaded onLoaded;

        public RectTransform rectTransform { get; private set; }

        GameObject scoreContainer;
        private float layoutScale = 1.0f;

        public const float staffPadding = 0.3f;
        public const float measurePadding = 0.5f;
        public const float noteAdvance = 0.75f;
        const float minimumAdavance = 0.25f;
        const float staffHeight = 2.29f;

        public void Awake()
        {
            multilineLayout = !overrideLineBreaks;
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

                layouts.Add(layout);
                layout.Init(multilineLayout);

                if (i == 0)
                    measureCount = layout.alignment.measures.Count;
                else if (layout.alignment.measures.Count != measureCount)
                        throw new LayoutException("All voices must have the same measure count");
            }
        }

        void LayoutScoreLine(int lineNum)
        {
            for (int measure = 0; measure < layouts[0].scoreLines[lineNum].measures.Count; measure++)
            {
                float advanceAmount = measurePadding;
                foreach (var layout in layouts)
                {
                    layout.beatIndex = 0;
                    var scoreLine = layout.scoreLines[lineNum];
                    var measureInfo = scoreLine.measures[measure];

                    measureInfo.container = new GameObject("Measure");
                }

                for (int beat = 1; beat <= timeSignature.beatCount; beat++)
                {
                    int beatItemIndex = 0;

                    while (true)
                    {
                        float maxBeatX = float.MinValue;
                        float alignment = 0.0f;
                        bool more = false;

                        foreach (var layout in layouts)
                        {
                            var scoreLine = layout.scoreLines[lineNum];
                            var layoutMeasure = scoreLine.measures[measure];
                            var beatInfo = layoutMeasure.source.beats[layout.beatIndex];

                            // if this beat is the start of a new group of notes render them
                            if (beatInfo.beatStart == beat && beatItemIndex < beatInfo.items.Count)
                            {
                                var element = layoutMeasure.AddItem(beatInfo.items[beatItemIndex]);

                                switch (element.item.type)
                                {
                                    case ABC.Item.Type.Note:
                                        CreateNoteSprite(layout.voice.clef, element);
                                        break;

                                    case ABC.Item.Type.Chord:
                                        CreateChordSprite(layout.voice.clef, element);
                                        break;

                                    case ABC.Item.Type.Rest:
                                        CreateRestSprite(element);
                                        break;

                                    case ABC.Item.Type.MultiMeasureRest:
                                        CreateMeasureRestSprite(element);
                                        break;
                                }

                                alignment = Mathf.Max(alignment, element.postfixAmount);
                            }
                        }

                        foreach (var layout in layouts)
                        {
                            var scoreLine = layout.scoreLines[lineNum];
                            var layoutMeasure = scoreLine.measures[measure];
                            var beatInfo = layoutMeasure.source.beats[layout.beatIndex];
                            if (beatInfo.beatStart == beat && beatItemIndex < beatInfo.items.Count)
                            {
                                var beatItem = layoutMeasure.elements[layoutMeasure.elements.Count - 1];
                                beatItem.alignOffset = alignment - beatItem.postfixAmount;

                                SetItemReferencePosition(beatItem, layoutMeasure, advanceAmount);
                                beatItem.container.transform.parent = layoutMeasure.container.transform;
                            }

                            maxBeatX = Math.Max(maxBeatX, layoutMeasure.bounds.size.x);
                        }

                        foreach (var layout in layouts)
                        {
                            var scoreLine = layout.scoreLines[lineNum];
                            var layoutMeasure = scoreLine.measures[measure];
                            var beatInfo = layoutMeasure.source.beats[layout.beatIndex];
                            if (beatInfo.beatStart == beat && beatItemIndex < beatInfo.items.Count)
                            {
                                //todo: This calculation may be incorrect if notes had different number of dots..
                                var delta = maxBeatX - layoutMeasure.insertX;
                                layoutMeasure.elements[layoutMeasure.elements.Count - 1].referencePosition += delta;

                                bool allItemsDone = beatItemIndex == beatInfo.items.Count - 1;
                                if (allItemsDone)
                                    layout.beatIndex += 1;
                                else
                                    more = true;
                            }

                            // in order to preserve alignment, all layouts will advance to the furthest position of the current beat marker
                            var measureInfo = layout.scoreLines[lineNum].measures[measure];
                            measureInfo.bounds.Encapsulate(new Vector3(maxBeatX, 0.0f, 0.0f));
                        }

                        beatItemIndex += 1;
                        advanceAmount = noteAdvance;
                        if (!more) break;
                    }
                }

                foreach (var layout in layouts)
                {
                    var layoutMeasure = layout.scoreLines[lineNum].measures[measure];
                    if (layoutMeasure.source.isRest)
                        CenterRestMeasure(layoutMeasure);

                    var element = layoutMeasure.AddItem(layoutMeasure.source.bar);
                    CreateBarSprite(element);
                    SetItemReferencePosition(element, layoutMeasure, advanceAmount);
                    element.container.transform.parent = layoutMeasure.container.transform;
                }
            }
        }

        void CenterRestMeasure(VoiceLayout.ScoreLine.Measure measure)
        {
            var item = measure.elements[0];
            var center = measure.insertX / 2.0f;
            var pos = center - item.info.totalBounding.size.x / 2.0f;
            item.referencePosition = pos;
        }

        void RenderScoreLine(int lineNum)
        {
            float startX = float.MinValue;

            foreach (var layout in layouts)
            {
                var scoreLine = layout.scoreLines[lineNum];
                scoreLine.container = new GameObject("ScoreLine");
                LayoutStaff(scoreLine, layout.voice.clef);

                startX = Mathf.Max(startX, scoreLine.insertX);
            }

            foreach (var layout in layouts)
            {
                var scoreLine = layout.scoreLines[lineNum];
                scoreLine.AdvaceInsertPos(startX - scoreLine.insertX + staffPadding);
            }

            if (lineNum == 0)
            {
                foreach (var layout in layouts)
                {
                    var scoreLine = layout.scoreLines[lineNum];
                    LayoutTimeSignature(scoreLine, layout.voice.initialTimeSignature);
                }
            }

            var measureWidths = CalculateMeasureWidths(lineNum);

            foreach (var layout in layouts)
            {
                var scoreLine = layout.scoreLines[lineNum];
                for (int i = 0; i < scoreLine.measures.Count; i++)
                {
                    var measure = scoreLine.measures[i];
                    var actualMeasureBounds = SetMeasureItemPositions(measure, measureWidths[i]);
                    measure.container.transform.parent = scoreLine.container.transform;
                    measure.container.transform.localPosition = scoreLine.insertPos;
                    scoreLine.EncapsulateAppendedBounds(actualMeasureBounds);
                }
            }

            FinalizeScoreLine(lineNum);
        }

        float[] CalculateMeasureWidths(int lineNum)
        {
            var scoreLine = layouts[0].scoreLines[lineNum];
            float[] measureWidths = new float[scoreLine.measures.Count];
            float referenceWidth = 0.0f;
            float allocatedSpace = horizontalMax - scoreLine.insertX;
            for (int i = 0; i < scoreLine.measures.Count; i++)
            {
                measureWidths[i] = scoreLine.measures[i].insertX;
                referenceWidth += measureWidths[i];
            }

            for (int i = 0; i < scoreLine.measures.Count; i++)
                measureWidths[i] = (measureWidths[i] / referenceWidth) * allocatedSpace;

            return measureWidths;
        }

        Bounds SetMeasureItemPositions(VoiceLayout.ScoreLine.Measure measure, float actualmeasureWidth)
        {
            Bounds actualBounds = new Bounds(Vector3.zero, Vector3.zero);
            List<Vector3> beamVertices = null;

            foreach (var item in measure.elements)
            {

                float positionX = (item.referencePosition / measure.insertX) * actualmeasureWidth;
                Vector3 insertPos = new Vector3(positionX - item.alignOffset - item.totalWidth, 0.0f, 0.0f);
                item.container.transform.localPosition = insertPos;
                actualBounds.Encapsulate(new Bounds(item.info.totalBounding.center + insertPos, item.info.totalBounding.size));

                var duration = item.item as ABC.Duration;
                if (duration != null && beams.TryGetValue(duration.beam, out Beam beam))
                {
                    var rootBounding = new Bounds(item.info.rootBounding.center + insertPos, item.info.rootBounding.size);
                    beam.Update(rootBounding);

                    if (beam.isReadyToCreate)
                    {
                        if (beam.type == Beam.Type.Angle)
                        {
                            if (beamVertices == null)
                                beamVertices = new List<Vector3>();

                            beam.CreateAngledBeam(beamVertices);
                        }
                        else
                        {
                            beam.CreateBasicBeam(cache, measure.container);
                        }
                    }
                }
            }

            if (beamVertices != null)
                Beam.CreateMesh(beamVertices, NoteMaterial, measure.container);

            return actualBounds;
        }

        class StafflineHeight
        {
            public readonly float min, max;
            public StafflineHeight(float min, float max)
            {
                this.min = min;
                this.max = max;
            }

            public float val { get { return max - min; } }
        }
        private static readonly Dictionary<ABC.Clef, StafflineHeight> baseStaffValues = new Dictionary<ABC.Clef, StafflineHeight>()
        {
            { ABC.Clef.Treble, new StafflineHeight(-0.9f, 3.0f) },
            { ABC.Clef.Bass, new StafflineHeight(0.0f, 2.3f) },
        };

        float CalculateScoreHeight()
        {
            float height = 0.0f;

            for (int i = 0; i < layouts[0].scoreLines.Count; i++)
            {
                //compute height of this score line
                for (int l = 0; l < layouts.Count; l++)
                {
                    var baseValues = baseStaffValues[layouts[l].voice.clef];
                    float min = baseValues.min;
                    float max = baseValues.max;

                    foreach (var measure in layouts[l].scoreLines[i].measures)
                    {
                        min = Mathf.Min(min, measure.bounds.min.y);
                        max = Mathf.Max(max, measure.bounds.max.y);
                    }

                    var staffSpacer = l == layouts.Count - 1 ? staffLineMargin : staffLinePadding;
                    height += (max - min) + staffSpacer;
                }
            }

            return height;
        }

        void PrepareScoreLines()
        {
            if (multilineLayout)
            {
                for (int i = 0; i < layouts[0].scoreLines.Count; i++)
                    LayoutScoreLine(i);
            }
            else
            {
                LayoutScoreLine(0);
                PartitionScoreLine();
            }

            float calculatedHeight = CalculateScoreHeight();

            if (calculatedHeight > rectTransform.rect.size.y)
                layoutScale = rectTransform.rect.size.y / calculatedHeight;
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

            scoreContainer = new GameObject("Score");
            scoreContainer.transform.parent = this.transform;
            scoreContainer.transform.localPosition = Vector3.zero;

            PrepareScoreLines();

            for (int i = 0; i < layouts[0].scoreLines.Count; i++)
                RenderScoreLine(i);

            scoreContainer.transform.localScale = new Vector3(layoutScale, layoutScale, layoutScale);
            this.gameObject.transform.localScale = scale;
            
        }

        /// <summary> Breaks up a single score line into multiple lines based on the horizontal max</summary>
        void PartitionScoreLine()
        {
            // save off the scorelines that were laid out
            int measureCount = layouts[0].scoreLines[0].measures.Count;
            var scoreLines = new List<VoiceLayout.ScoreLine.Measure>[layouts.Count];
            for (int i = 0; i < scoreLines.Length; i++)
            {
                scoreLines[i] = layouts[i].scoreLines[0].measures;
                layouts[i].scoreLines.Clear();
                layouts[i].scoreLines.Add(new VoiceLayout.ScoreLine());
            }

            float currentWidth = 0.0f;

            for (int measureIndex = 0; measureIndex < measureCount; measureIndex++)
            {
                float measureWidth = float.MinValue;
                foreach (var scoreLine in scoreLines)
                    measureWidth = Mathf.Max(measureWidth, scoreLine[measureIndex].insertX);

                // ensure that the measure of each scoreline will fit on the current line.
                // If it wont fit make a new line
                foreach (var scoreLine in scoreLines)
                {
                    if (currentWidth + scoreLine[measureIndex].insertX > horizontalMax)
                    {
                        foreach (var layout in layouts)
                            layout.scoreLines.Add(new VoiceLayout.ScoreLine());

                        currentWidth = 0.0f;
                        break;
                    }
                }

                //Add the current measure to the last scoreline
                for (int i = 0; i < scoreLines.Length; i++)
                {
                    var scoreLine = layouts[i].scoreLines[layouts[i].scoreLines.Count - 1];
                    scoreLine.measures.Add(scoreLines[i][measureIndex]);
                    currentWidth += measureWidth;
                }
            }
        }

        TimeSignature GetTimeSignature()
        {
            TimeSignature result = null;

            for (int i = 0; i < tune.voices.Count; i++)
            {
                var initialTimeSignature = tune.voices[i].initialTimeSignature;
                if (initialTimeSignature == string.Empty)
                    throw new BeatAlignmentException($"Voice {i} does not initially declare a time signature.");

                var timeSignature = TimeSignature.Parse(initialTimeSignature);

                if (result == null)
                    result = timeSignature;
                else if (!timeSignature.Equals(result))
                    throw new LayoutException("All voices should have the same time signature");

            }

            return result;
        }

        /// <summary>Calculates the final size of the staff and positions it correctly relative to the container.</summary>
        void FinalizeScoreLine(int lineNum)
        {
            //foreach (var layout in layouts)
            for (int i = 0; i < layouts.Count; i++)
            {
                var scoreLine = layouts[i].scoreLines[lineNum];
                AdjustStaffScale(scoreLine);

                scoreLine.container.transform.parent = scoreContainer.transform;
                scoreLine.container.transform.localPosition = new Vector3(staffOffset.x, staffOffset.y - (scoreLine.bounds.max.y), 0.0f);
                var staffSpacer = i == layouts.Count - 1 ? staffLineMargin : staffLinePadding;
                staffOffset.y -= (scoreLine.bounds.size.y + staffSpacer);
            }

            RenderConnectorBar(lineNum);
        }

        void RenderConnectorBar(int lineNum)
        {
            var barSprite = cache.GetSpriteObject("Bar_Line");
            barSprite.transform.parent = scoreContainer.transform;

            var topScoreLine = layouts[0].scoreLines[lineNum];
            var bottomScoreLine = layouts[layouts.Count - 1].scoreLines[lineNum];

            var barPos = bottomScoreLine.container.transform.localPosition;
            var topPos = topScoreLine.container.transform.localPosition.y + staffHeight;

            var targetHeight = topPos - barPos.y;
            var scale = targetHeight / barSprite.bounds.size.y;

            barSprite.transform.localPosition = barPos;
            barSprite.transform.localScale = new Vector3(1.0f, scale, 1.0f);
        }

        void LayoutStaff(VoiceLayout.ScoreLine scoreLine, ABC.Clef clef)
        {
            var staffSprite = cache.GetSpriteObject("Staff");
            staffSprite.transform.parent = scoreLine.container.transform;
            staffSprite.transform.localPosition = scoreLine.insertPos;
            scoreLine.bounds = staffSprite.bounds;

            scoreLine.AdvaceInsertPos(staffPadding);
            var clefSprite = cache.GetSpriteObject($"Clef_{clef}");
            clefSprite.transform.parent = scoreLine.container.transform;
            clefSprite.transform.localPosition = scoreLine.insertPos;
            scoreLine.bounds.Encapsulate(clefSprite.bounds);
            scoreLine.AdvaceInsertPos(staffPadding);
        }
        
        void LayoutTimeSignature(VoiceLayout.ScoreLine scoreLine, string timeSignature)
        {
            var container = new GameObject("Time Signature");
            container.transform.parent = scoreLine.container.transform;

            var timeSignatureInfo = notes.CreateTimeSignature(timeSignature, container);
            
            container.transform.localPosition = scoreLine.insertPos;
            scoreLine.EncapsulateAppendedBounds(timeSignatureInfo.totalBounding);
        }

        void AdjustStaffScale(VoiceLayout.ScoreLine scoreLine)
        {
            var currentStaff = scoreLine.container.transform.GetChild(0);
            var currentWidth = currentStaff.GetComponent<SpriteRenderer>().bounds.size.x;
            var scaleX = scoreLine.insertX / currentWidth;
            currentStaff.transform.localScale = new Vector3(scaleX, 1.0f, 1.0f);
        }
        
        void SetItemReferencePosition(VoiceLayout.ScoreLine.Element element, VoiceLayout.ScoreLine.Measure measure, float advanceAmount)
        {
            float spacer = Mathf.Max(minimumAdavance, advanceAmount - element.prefixAmount);
            
            measure.AdvaceInsertPos(spacer);
            
            measure.EncapsulateAppendedBounds(element.info.totalBounding);
            element.referencePosition = measure.insertX;
            Debug.Log($"SetItemReferencePosition: {spacer}, {element.item}, {element.referencePosition}");
        }

        void CreateChordSprite(ABC.Clef clef, VoiceLayout.ScoreLine.Element element)
        {
            var chordItem = element.item as ABC.Chord;
            element.container = new GameObject("Chord");
            
            tune.decorations.TryGetValue(chordItem.id, out var decorations);
            
            NoteInfo chordInfo;
            if (beams.TryGetValue(chordItem.beam, out Beam beam))
                chordInfo = notes.CreateChord(chordItem, beam, decorations, element.container);
            else
                chordInfo = notes.CreateChord(chordItem, clef, decorations, element.container);
            
            element.info = chordInfo;
        }

        void CreateNoteSprite(ABC.Clef clef, VoiceLayout.ScoreLine.Element element)
        {
            var noteItem = element.item as ABC.Note;
            element.container = new GameObject("Note");

            tune.decorations.TryGetValue(noteItem.id, out var decorations);

            NoteInfo noteInfo;
            if (beams.TryGetValue(noteItem.beam, out Beam beam))
                noteInfo = notes.CreateNote(noteItem, beam, decorations, element.container);
            else
                noteInfo = notes.CreateNote(noteItem, clef, decorations, element.container);
            
            element.info = noteInfo;
        }

        void CreateRestSprite(VoiceLayout.ScoreLine.Element element)
        {
            var restItem = element.item as ABC.Rest;
            element.container = new GameObject("Rest");
            element.info = notes.CreateRest(restItem, element.container);
        }

        void CreateBarSprite(VoiceLayout.ScoreLine.Element element)
        {
            var barItem = element.item as ABC.Bar;
            element.container = new GameObject("Bar");
            element.info = notes.CreateBar(barItem, element.container);
        }

        void CreateMeasureRestSprite(VoiceLayout.ScoreLine.Element element)
        {
            var measureRest = element.item as ABC.MultiMeasureRest;
            element.container = new GameObject("Rest");
            element.info = notes.CreateMeasureRest(measureRest, element.container);
        }
    }
}

