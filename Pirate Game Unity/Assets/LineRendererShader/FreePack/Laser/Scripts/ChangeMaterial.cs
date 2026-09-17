using System.Collections.Generic;
using UnityEngine;
namespace Oicaimang
{
    public class ChangeMaterial : MonoBehaviour
    {
        [SerializeField] Material[] materials;
        [SerializeField] float changeInterval = 5f;

        [SerializeField] List<LineRenderer> lines;
        private int currentIndex = 0;
        private float timer = 0f;

        void Start()
        {
            if (materials.Length > 0)
            {
                foreach (var line in lines)
                {
                    line.material = materials[0];
                }
            }
        }

        void Update()
        {
            if (materials.Length == 0) return;

            timer += Time.deltaTime;

            if (timer >= changeInterval)
            {
                timer = 0f;
                currentIndex = (currentIndex + 1) % materials.Length;
                foreach (var line in lines)
                {
                    line.material = materials[currentIndex];
                }
            }
        }
    }
}