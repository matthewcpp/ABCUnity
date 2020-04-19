using UnityEngine;
using System.Collections.Generic;

namespace ABCUnity
{
    class Beam
    {
        const float defaultStemHeight = 1.92f;
        const float accidentOffset = -0.493f;
        static Vector3 beamOffset = new Vector3(0.0f, 0.366f, 0.0f);

        static Vector3 stemUpOffset = new Vector3(0.65f, -0.35f, 0.0f);
        static Vector3 eighthBeamUpOffset = new Vector3(0.706f, 2.13f, 0.0f);
        
        static Vector3 stemDownOffset = new Vector3(0.0f, 0.262f, 0.0f);
        static Vector3 eighthBeamDownOffset = new Vector3(0.055f, -1.516f, 0.0f);

        public NoteCreator.NoteDirection noteDirection { get; private set; }
        public enum Type {
            /// <summary> All the notes contain the same pitch need to render a straight bar</summary>
            Uniform
        }

        public List<ABC.Item> items { get; } = new List<ABC.Item>();

        public Type type { get; private set; }

        int id { get; }

        public ABC.Clef clef { get; }

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
            type = Type.Uniform;
        }

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

        SpriteRenderer first;

        public void Update(SpriteRenderer sprite, SpriteCache cache, GameObject container)
        {
            if (index == 0)
                first = sprite;

            index += 1;

            if (index == items.Count)
            {
                var bar = cache.GetSpriteObject($"Note_Bar_{noteDirection}");
                bar.transform.parent = container.transform;

                float distX;
                if (noteDirection == NoteCreator.NoteDirection.Up)
                {
                    var barPos = first.bounds.max;
                    bar.transform.position = barPos;

                    distX = sprite.bounds.max.x - barPos.x;
                }
                else
                {
                    var barPos = first.bounds.min;
                    bar.transform.position = barPos;

                    distX = sprite.bounds.min.x - barPos.x;
                }

                bar.transform.localScale = new Vector3(distX, 1.0f, 1.0f);
            }
        }
    }
}