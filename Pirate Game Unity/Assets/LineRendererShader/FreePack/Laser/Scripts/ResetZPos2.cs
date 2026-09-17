using System.Collections.Generic;
using UnityEngine;
namespace Oicaimang
{
    public class ResetZPos2 : MonoBehaviour
    {
        [SerializeField] List<LineRenderer> lines;
        [ContextMenu("ResetZPos")]
        public void Reset()
        {
            foreach (var line in lines)
            {
                Vector3[] positions = new Vector3[line.positionCount];
                line.GetPositions(positions);
                for (int i = 0; i < positions.Length; i++)
                {
                    positions[i] = new Vector3(positions[i].x, positions[i].y, 0);
                }
                line.SetPositions(positions);

            }
        }
    }
}