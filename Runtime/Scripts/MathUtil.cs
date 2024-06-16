using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ABCUnity
{
    public static class MathUtil
    {
        public static float[,] ReducedRowEchelonForm(float[,] matrix)
        {            
            int lead = 0, rowCount = matrix.GetLength(0), columnCount = matrix.GetLength(1);
            for (int r = 0; r < rowCount; r++)
            {
                if (columnCount <= lead) break;
                int i = r;
                while (matrix[i, lead] == 0)
                {
                    i++;
                    if (i == rowCount)
                    {
                        i = r;
                        lead++;
                        if (columnCount == lead)
                        {
                        lead--;
                        break;
                        }
                    }
                }
                for (int j = 0; j < columnCount; j++)
                {
                    float temp = matrix[r, j];
                    matrix[r, j] = matrix[i, j];
                    matrix[i, j] = temp;
                }
                float div = matrix[r, lead];
                if(div != 0)
                    for (int j = 0; j < columnCount; j++) matrix[r, j] /= div;                
                for (int j = 0; j < rowCount; j++)
                {
                    if (j != r)
                    {
                        float sub = matrix[j, lead];
                        for (int k = 0; k < columnCount; k++) matrix[j, k] -= (sub * matrix[r, k]);
                    }
                }
                lead++;
            }
            return matrix;
        }

        public static Vector3 LineIntersect(Vector3 line1V1, Vector3 line1V2, Vector3 line2V1, Vector3 line2V2)
        {
            //Line1
            float A1 = line1V2.y - line1V1.y;
            float B1 = line1V1.x - line1V2.x;
            float C1 = A1*line1V1.x + B1*line1V1.y;

            //Line2
            float A2 = line2V2.y - line2V1.y;
            float B2 = line2V1.x - line2V2.x;
            float C2 = A2 * line2V1.x + B2 * line2V1.y;

            float det = A1*B2 - A2*B1;
            if (det == 0)
            {
                return Vector3.zero; //parallel lines
            }

            float x = (B2*C1 - B1*C2)/det;
            float y = (A1 * C2 - A2 * C1) / det;

            return new Vector3(x,y,0);
        }
    }
}