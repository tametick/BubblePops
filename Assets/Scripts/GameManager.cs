using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour {
	private void Awake() {
		DG.Tweening.DOTween.Init();
	}

	public void RestartGame() {
		SceneManager.LoadScene("Main");
	}
}
