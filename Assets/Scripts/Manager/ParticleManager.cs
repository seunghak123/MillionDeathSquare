using Cysharp.Threading.Tasks;
using Seunghak;
using Seunghak.Common;
using Seunghak.UIManager;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
namespace Seunghak.Common
{
    public class ParticleManager : UnitySingleton<ParticleManager>
    {
        private List<string> spawnedParticleNameLists = new List<string>();
        private int totalCount = 0;
        private int currentCount = 0;
        public bool IsFinish()
        {
            return totalCount <= currentCount;
        }
        public async UniTask PreloadParticleObject(string particleName, int count)
        {
            totalCount = count;
            currentCount = 0;

            for (int i = 0; i < count; i++)
            {
                //SpawnParticleObject(particleName, ParticleManager.Instance.gameObject.transform);
                currentCount = i + 1;
            }
        }

        //public ParticleObject SpawnParticleObject(string particleName, Transform targetParent = null)
        //{
        //    if (!spawnedParticleNameLists.Contains(particleName))
        //    {
        //        spawnedParticleNameLists.Add(particleName);
        //    }
        //    GameObject spawnParticle = GameResourceManager.Instance.SpawnObject(particleName);
        //    if(spawnParticle != null)
        //    {
        //        if (targetParent != null)
        //        {
        //            spawnParticle.transform.position = targetParent.position;
        //            spawnParticle.transform.parent = UIManager.UIManager.Instance.GetEffectParent();
        //        }
        //        else
        //        {
        //            spawnParticle.transform.localPosition = Vector3.zero;
        //            spawnParticle.transform.parent = UIManager.UIManager.Instance.GetEffectParent();
        //        }
        //    }
        //    return spawnParticle.GetComponent<ParticleObject>();
        //}
        public void ReleaseAllParticleObject()
        {
            for (int i = 0; i < spawnedParticleNameLists.Count; i++)
            {
                GameResourceManager.Instance.ReleasePool(spawnedParticleNameLists[i]);
            }
            spawnedParticleNameLists.Clear();
        }
    }
}
