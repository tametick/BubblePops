using DG.Tweening;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Level : MonoBehaviour {
	// the logical grid of bubbles
	private Dictionary<IntVector2, Bubble> bubbles;

	[SerializeField]
	private GameObject bubblePrefab;
	[SerializeField]
	internal Launcher launcher;

	[SerializeField]
	// don't move initial level position more than this many rows down
	private int maxRowsToShift;
	[SerializeField]
	private int rows;
	[SerializeField]
	private int columns;
	private Collider topWall;

	[SerializeField]
	private TextMeshProUGUI bonusText;

	internal void AddBubble(IntVector2 gridPos, Bubble b) {
		bubbles[gridPos] = b;

		// make sure we are set to collide when part of the level, to collide with launched bubbles
		b.SetTrigger(false);
	}
	internal void RemoveBubble(IntVector2 gridpos) {
		bubbles.Remove(gridpos);
	}

	// how many times do we shift the level up after a player action
	private int numberOfDownShifts;
	private void Start() {
		bonusText.gameObject.SetActive(false);
		topWall = GetComponentInChildren<Collider>();
		topWall.transform.Translate(Vector3.up * rows);

		// instantiate all bubbles
		bubbles = new Dictionary<IntVector2, Bubble>();
		for (int x = 0; x < columns; x++) {
			for (int y = 0; y < rows; y++) {
				Vector3 pos = GridToWorld(x, y);
				GameObject bubbleGO = GameObject.Instantiate(bubblePrefab, pos, Quaternion.identity, transform);
				AddBubble(new IntVector2(x, y), bubbleGO.GetComponent<Bubble>());
			}
		}

		// shift level down so that the bottom row is near the bubble-launcher
		if (maxRowsToShift > rows) {
			transform.position = Vector3.down * rows;
			numberOfDownShifts = 0;
		} else {
			transform.position = Vector3.down * maxRowsToShift;
			numberOfDownShifts = rows - maxRowsToShift;
		}

		// init bubbles
		foreach (KeyValuePair<IntVector2, Bubble> gridBubble in bubbles) {
			Bubble parentBubble = null;
			IntVector2 gridPos = gridBubble.Key;
			// top bubble are jointed to the level, the rest to the bubble on top of them
			if (gridPos.y < rows - 1) {
				parentBubble = bubbles[new IntVector2(gridPos.x, gridPos.y + 1)];
			}

			gridBubble.Value.Init(this, gridBubble.Key, parentBubble, Bubble.GetRandomExponent());
		}
	}

	internal void FinishedMerging() {
		launcher.Reload();
	}

	internal IntVector2 WorldToGrid(Vector3 worldPosition) {
		Vector3 local = transform.InverseTransformPoint(worldPosition);
		int gridY = Mathf.RoundToInt(local.y);
		int gridX = gridY % 2 == 0 ?
				Mathf.RoundToInt(local.x) :
				Mathf.RoundToInt(local.x - 0.5f);
		return new IntVector2(gridX, gridY);
	}

	internal Vector3 GridToWorld(IntVector2 gridPos) {
		return GridToWorld(gridPos.x, gridPos.y);
	}
	internal Vector3 GridToWorld(int gridX, int gridY) {
		return transform.TransformPoint(new Vector3(gridX + (gridY % 2 == 0 ? 0 : 0.5f), gridY));
	}

	internal Bubble GetBubble(IntVector2 gridPos) {
		// get a bubble via logical grid position, return null if out of bounds
		try {
			return bubbles[gridPos];
		} catch (KeyNotFoundException) {
			return null;
		}
	}

	internal Bubble GetBubble(int x, int y) {
		return GetBubble(new IntVector2(x, y));
	}

	internal List<Bubble> GetNeighbors(int x, int y) {
		List<Bubble> neighbors = new List<Bubble>();

		if (y % 2 == 0) {
			// for even rows
			// above
			neighbors.Add(GetBubble(x - 1, y + 1));
			neighbors.Add(GetBubble(x, y + 1));

			// sides
			neighbors.Add(GetBubble(x - 1, y));
			neighbors.Add(GetBubble(x + 1, y));

			// below
			neighbors.Add(GetBubble(x - 1, y - 1));
			neighbors.Add(GetBubble(x, y - 1));
		} else {
			// for odd rows
			// above
			neighbors.Add(GetBubble(x, y + 1));
			neighbors.Add(GetBubble(x + 1, y + 1));

			// sides
			neighbors.Add(GetBubble(x - 1, y));
			neighbors.Add(GetBubble(x + 1, y));

			// below
			neighbors.Add(GetBubble(x, y - 1));
			neighbors.Add(GetBubble(x + 1, y - 1));
		}

		// filter out the out of bounds/null neighbors
		return neighbors.FindAll(n => n != null);
	}

	internal void ShiftDownIfNeeded() {
		if (numberOfDownShifts > 0) {
			numberOfDownShifts--;
			transform.DOMoveY(transform.position.y - 1, 0.25f);
		}
	}

	internal bool IsCleared() {
		return bubbles.Values.Count == 0;
	}
	private void OnEnable() {
		Launcher.OnClear += ShowPerfect;
	}
	private void OnDisable() {
		Launcher.OnClear -= ShowPerfect;
	}

	internal void ShowPerfect() {
		bonusText.transform.localScale = Vector3.zero;
		bonusText.gameObject.SetActive(true);
		bonusText.text = "Perfect!";

		bonusText.transform
			.DOScale(1, .5f);
	}
}
