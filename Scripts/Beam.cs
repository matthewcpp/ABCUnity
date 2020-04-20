using UnityEngine;
using System.Collections.Generic;

namespace ABCUnity
{
    class Beam
    {
        const float defaultStemHeight = 1.92f;
        public static Vector3 stemUpOffset = new Vector3(0.65f, 0.35f, 0.0f);
        public static Vector3 stemDownOffset = new Vector3(0.0f, 0.262f, 0.0f);

        const float beamHeight = 0.28f;
        static Vector3 beamOffset = new Vector3(0.0f, 0.366f, 0.0f);
        const float defaultBeamSpacer = 0.2f;

        public NoteCreator.NoteDirection noteDirection { get; private set; }
        public enum Type {
            /// <summary> All the notes contain the same pitch need to render a straight bar.</summary>
            Basic,

            /// <summary> All Notes are either increasing or decreasing in pitch.</summary>
            Angle,

            /// <summary> Notes are connected by a straight beam.</summary>
            Straight
        }

        public List<ABC.Item> items { get; } = new List<ABC.Item>();

        public Type type { get; private set; }

        int id { get; }

        public ABC.Clef clef { get; }

        public float stemHeight { get; set; } = 0.0f;

        private int index = 0;

        public Beam(int id, ABC.Clef clef)
        {
            this.id = id;
            this.clef = clef;
        }

        /// <summary> Analyzes the items in the beam and determines relevant info that will control layout. </summary>
        public void Analyze()
        {
            DetermineNoteDirection();

            if (IsBasic())
                type = Type.Basic;
            else if (IsAngled())
                type = Type.Angle;
            else
            {
                type = Type.Straight;
                DetermineStemHeight();
            }
        }

        /// <summary> 
        /// Determines the stem height to use for a straight beam.
        /// The Height is calculated as the default terminus of the highest note's stem
        /// </summary>
        void DetermineStemHeight()
        {
            if (noteDirection == NoteCreator.NoteDirection.Up)
            {
                ABC.Pitch pitch = ABC.Pitch.A0;
                foreach (var item in items)
                {
                    var note = item as ABC.Note;
                    if (note.pitch > pitch)
                        pitch = note.pitch;
                }

                int stepCount = pitch - NoteCreator.clefZero[clef];
                stemHeight = NoteCreator.noteStep * stepCount + defaultStemHeight;
            }
            else
            {
                ABC.Pitch pitch = ABC.Pitch.C8;
                foreach (var item in items)
                {
                    var note = item as ABC.Note;
                    if (note.pitch < pitch)
                        pitch = note.pitch;
                }

                int stepCount = pitch - NoteCreator.clefZero[clef];
                stemHeight = NoteCreator.noteStep * (stepCount + 1) - defaultStemHeight;
            }
        }

        /// <summary>
        /// Determines the direction of the beam stem.
        /// If the average of all notes is above the middle staff value they will be down else, up.
        /// </summary>
        private void DetermineNoteDirection()
        {
            int sum = 0;
            int count = 0;
            foreach (var item in items)
            {
                switch (item.type)
                {
                    case ABC.Item.Type.Note:
                        var noteItem = item as ABC.Note;
                        sum += (int)noteItem.pitch;
                        count += 1;
                        break;

                    case ABC.Item.Type.Chord:
                        var chordItem = item as ABC.Chord;
                        foreach (var note in chordItem.notes)
                            sum += (int)note.pitch;

                        count += chordItem.notes.Length;
                        break;

                    default:
                        throw new LayoutException($"Unexpected item of type: {item.type} found in beam {id}");
                }
            }

            float averagePitch = sum / (float)count;
            noteDirection = (averagePitch > (float)NoteCreator.clefZero[clef] + 3) ? NoteCreator.NoteDirection.Down : NoteCreator.NoteDirection.Up;
        }

        Bounds first;

        /// <summary>
        /// This method is called every time an item in the beam has been laid out.  
        /// When the final item is laid out the beam will be added to the scene.
        /// </summary>
        public void Update(Bounds bounds, SpriteCache cache, VoiceLayout layout)
        {
            if (index == 0)
                first = bounds;

            index += 1;

            if (index == items.Count)
            {
                switch(type)
                {
                    case Type.Basic:
                    case Type.Straight:
                        CreateBasicBeam(bounds, cache, layout.measure.container);
                        break;

                    case Type.Angle:
                        CreateAngledBeam(bounds, layout.measureVertices);
                        break;
                }
            }
        }

        bool IsBasic()
        {
            var firstNote = items[0] as ABC.Note;

            for (int i = 0; i < items.Count; i++)
            {
                var currentNote = items[i] as ABC.Note;

                if (currentNote.pitch != firstNote.pitch)
                    return false;
            }

            return true;
        }

        bool IsAngled()
        {
            var previousNote = items[0] as ABC.Note;
            var currentNote = items[1] as ABC.Note;

            if (currentNote.pitch > previousNote.pitch)
            {
                previousNote = currentNote;

                for (int i = 2; i < items.Count; i++)
                {
                    currentNote = items[i] as ABC.Note;

                    if (currentNote.pitch <= previousNote.pitch)
                        return false;

                    previousNote = currentNote;
                }

                return true;
            }
            else if (currentNote.pitch < previousNote.pitch)
            {
                previousNote = currentNote;

                for (int i = 2; i < items.Count; i++)
                {
                    currentNote = items[i] as ABC.Note;

                    if (currentNote.pitch >= previousNote.pitch)
                        return false;

                    previousNote = currentNote;
                }
            }

            return false;
        }

        /// <summary>
        /// Creates a basic straight beam connecting the first and last item in this beam.
        /// Note that this method should be used when the first and last items have the same max Y bounding value.
        /// </summary>
        private void CreateBasicBeam(Bounds sprite, SpriteCache cache, GameObject container)
        {
            float offsetY = 0.0f;
            ABC.Length length = (items[0] as ABC.Duration).length;

            for (ABC.Length l = ABC.Length.Eighth; l >= length; l--)
            {
                var beam = cache.GetSpriteObject($"Note_Beam_{noteDirection}");
                beam.transform.parent = container.transform;

                float distX;
                if (noteDirection == NoteCreator.NoteDirection.Up)
                {
                    var beamPos = first.max;
                    beam.transform.position = new Vector3(beamPos.x, beamPos.y - offsetY, 0.0f);

                    distX = sprite.max.x - beamPos.x;
                }
                else
                {
                    var beamPos = first.min;
                    beam.transform.position = new Vector3(beamPos.x, beamPos.y + offsetY, 0.0f); ;

                    distX = sprite.min.x - beamPos.x;
                }

                beam.transform.localScale = new Vector3(distX, 1.0f, 1.0f);

                offsetY += beamHeight + defaultBeamSpacer;
            }
        }

        /// <summary>
        /// Creates a beam connecting the first and last item in this beam.
        /// This method should be used when the max values for these items are not at the same height.
        /// Mesh vertices are generated as opposed to a sprite as in the straight beam.
        /// </summary>
        private void CreateAngledBeam(Bounds sprite, List<Vector3> meshVertices)
        {
            float offsetY = 0.0f;
            ABC.Length length = (items[0] as ABC.Duration).length;

            for (ABC.Length l = ABC.Length.Eighth; l >= length; l--)
            {
                if (noteDirection == NoteCreator.NoteDirection.Up)
                {
                    meshVertices.Add(new Vector3(first.max.x, first.max.y - offsetY, 0.0f));
                    meshVertices.Add(new Vector3(sprite.max.x, sprite.max.y - offsetY, 0.0f));
                    meshVertices.Add(new Vector3(first.max.x, first.max.y - offsetY - beamHeight, 0.0f));
                    meshVertices.Add(new Vector3(sprite.max.x, sprite.max.y - offsetY - beamHeight, 0.0f));
                }
                else
                {
                    meshVertices.Add(new Vector3(first.min.x, first.min.y + offsetY + beamHeight, 0.0f));
                    meshVertices.Add(new Vector3(sprite.min.x, sprite.min.y + offsetY + beamHeight, 0.0f));
                    meshVertices.Add(new Vector3(first.min.x, first.min.y + offsetY, 0.0f));
                    meshVertices.Add(new Vector3(sprite.min.x, sprite.min.y + offsetY, 0.0f));
                }

                offsetY += beamHeight + defaultBeamSpacer;
            }
        }
    }
}