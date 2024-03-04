using UnityEngine;
using System.Collections.Generic;

namespace ABCUnity
{
    static class Slur
    {
        public static LineRenderer CreateSingleScorelineSlur(VoiceLayout.ScoreLine scoreLine, VoiceLayout.ScoreLine.Element startElement, VoiceLayout.ScoreLine.Element endElement, Material material)
        {
            var slurPositions = new List<Vector3>();

            var startPos = new Vector3(startElement.info.rootBounding.max.x, startElement.info.rootBounding.min.y, 0.0f);
            startPos += startElement.container.transform.localPosition + startElement.measure.container.transform.localPosition;
            startPos += new Vector3(0.1f, -0.1f);

            var endPos = endElement.info.rootBounding.min;
            endPos += endElement.container.transform.localPosition + endElement.measure.container.transform.localPosition;
            endPos += new Vector3(-0.1f, -0.1f);

            var midpoint = (startPos + endPos) / 2.0f;
            midpoint.y -= 0.3f;

            slurPositions.Add(startPos);

            float[,] matrix = new float[3, 4]{
                { startPos.x * startPos.x, startPos.x, 1,  startPos.y },
                { midpoint.x * midpoint.x, midpoint.x, 1,  midpoint.y },
                { endPos.x * endPos.x, endPos.x, 1,  endPos.y  }
            };

            matrix = MatrixUtil.ReducedRowEchelonForm(matrix);
            float a = matrix[0, 3];
            float b = matrix[1, 3];
            float c = matrix[2, 3];

            const int segmentCount = 20;
            float step = (endPos.x - startPos.x) / segmentCount;
            float x = startPos.x;
            for (int i = 0; i < segmentCount; i++) {
                x += step;

                float y = a * (x*x) + b * x + c;

                slurPositions.Add(new Vector3(x, y, 0));
            }

            return CreateLineRenderer(scoreLine, slurPositions, material);
        }

        private static LineRenderer CreateLineRenderer(VoiceLayout.ScoreLine scoreLine, List<Vector3> slurPositions, Material material)
        {
            if (scoreLine.slurs == null) {
                scoreLine.slurs = new GameObject("Slurs");
                scoreLine.slurs.transform.SetParent(scoreLine.container.transform, false);
            }

            var lineRenderer = scoreLine.slurs.AddComponent<LineRenderer>();
            lineRenderer.positionCount = slurPositions.Count;
            lineRenderer.SetPositions(slurPositions.ToArray());
            
            lineRenderer.useWorldSpace = false;
            lineRenderer.startWidth = 0.1f;
            lineRenderer.endWidth = 0.1f;

            lineRenderer.material = material;

            return lineRenderer;
        }
    }
}