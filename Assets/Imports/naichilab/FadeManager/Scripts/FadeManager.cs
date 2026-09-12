using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// シーン遷移時のフェードイン・アウトを制御するためのクラス .
/// </summary>
public class FadeManager : MonoBehaviour
{

	#region Singleton

	private static FadeManager instance;

	public static FadeManager Instance {
		get {
			if (instance == null) {
				instance = FindAnyObjectByType<FadeManager> ();

				if (instance == null) {
					Debug.LogError (typeof(FadeManager) + "is nothing");
				}
			}

			return instance;
		}
	}

	#endregion Singleton

	/// <summary>
	/// デバッグモード .
	/// </summary>
	public bool DebugMode = true;
	/// <summary>フェード中の透明度</summary>
	private float fadeAlpha = 0;
	/// <summary>フェード中かどうか</summary>
	private bool isFading = false;
	/// <summary>フェード色</summary>
	public Color fadeColor = Color.black;


	public void Awake ()
	{
		// ランタイムで初めて生成したときは Instance 検索が自分を拾えず破棄されることがあるため、先に代入する
		if (instance == null) {
			instance = this;
		} else if (this != instance) {
			Destroy (this.gameObject);
			return;
		}

		DontDestroyOnLoad (this.gameObject);
	}

	public void OnGUI ()
	{

		// Fade（DrawTexture は Repaint のみ）。Layout でも OnGUI が呼ばれるため、毎フェーズで DrawTexture すると GUI.depth の変動ログが出やすい。
		if (this.isFading && Event.current.type == EventType.Repaint) {
			this.fadeColor.a = this.fadeAlpha;
			GUI.color = this.fadeColor;
			GUI.DrawTexture (new Rect (0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
		}

		if (this.DebugMode) {
			if (!this.isFading) {
				//Scene一覧を作成 .
				//(UnityEditor名前空間を使わないと自動取得できなかったので決めうちで作成) .
				List<string> scenes = new List<string> ();
				scenes.Add ("SampleScene");
				//scenes.Add ("SomeScene1");
				//scenes.Add ("SomeScene2");


				//Sceneが一つもない .
				if (scenes.Count == 0) {
					GUI.Box (new Rect (10, 10, 200, 50), "Fade Manager(Debug Mode)");
					GUI.Label (new Rect (20, 35, 180, 20), "Scene not found.");
					return;
				}


				GUI.Box (new Rect (10, 10, 300, 50 + scenes.Count * 25), "Fade Manager(Debug Mode)");
				GUI.Label (new Rect (20, 30, 280, 20), "Current Scene : " + SceneManager.GetActiveScene ().name);

				int i = 0;
				foreach (string sceneName in scenes) {
					if (GUI.Button (new Rect (20, 55 + i * 25, 100, 20), "Load Level")) {
						LoadScene (sceneName, 1.0f);
					}
					GUI.Label (new Rect (125, 55 + i * 25, 1000, 20), sceneName);
					i++;
				}
			}
		}



	}

	/// <summary>
	/// 画面遷移 .
	/// </summary>
	/// <param name='scene'>シーン名</param>
	/// <param name='interval'>暗転にかかる時間(秒)</param>
	public void LoadScene (string scene, float interval)
	{
		StartCoroutine (TransScene (scene, interval, interval));
	}

	/// <summary>
	/// フェードアウトとフェードインの秒数を別々に指定して画面遷移します。
	/// </summary>
	public void LoadScene (string scene, float fadeOutSeconds, float fadeInSeconds)
	{
		StartCoroutine (TransScene (scene, fadeOutSeconds, fadeInSeconds));
	}

	/// <summary>
	/// シーン遷移用コルーチン .
	/// </summary>
	private IEnumerator TransScene (string scene, float fadeOutInterval, float fadeInInterval)
	{
		fadeOutInterval = Mathf.Max (0.0001f, fadeOutInterval);
		fadeInInterval = Mathf.Max (0.0001f, fadeInInterval);

		//だんだん暗く .
		this.isFading = true;
		float time = 0;
		while (time <= fadeOutInterval) {
			this.fadeAlpha = Mathf.Lerp (0f, 1f, time / fadeOutInterval);
			time += Time.deltaTime;
			yield return 0;
		}

		//シーン切替 .
		SceneManager.LoadScene (scene);

		//だんだん明るく .
		time = 0;
		while (time <= fadeInInterval) {
			this.fadeAlpha = Mathf.Lerp (1f, 0f, time / fadeInInterval);
			time += Time.deltaTime;
			yield return 0;
		}

		this.isFading = false;
	}
}
