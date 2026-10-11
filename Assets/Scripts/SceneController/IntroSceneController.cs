using System;
using UnityEngine;
using UnityEngine.Video;

namespace Seunghak.SceneManager
{
    public class IntroSceneController : MonoBehaviour
    {
        [SerializeField] private Animation introPlayer;
        protected void Awake()
        {
            introPlayer.Play();
        }
        bool isNext = false;
        protected void Update()
        {
            if (!isNext&& !introPlayer.isPlaying)
            {
                isNext = true;
                ChangeScene();
            }
        }
        public void ChangeScene()
        {
            UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("TITLE");
        }
    }
}
