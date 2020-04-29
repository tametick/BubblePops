internal class IntVector2 {
	internal readonly int x, y;

	internal IntVector2(int x, int y) {
		this.x = x;
		this.y = y;
	}

	public override int GetHashCode() {
		return $"{x},{y}".GetHashCode();
	}
	public override bool Equals(object obj) {
		return Equals(obj as IntVector2);
	}
	public bool Equals(IntVector2 obj) {
		return obj != null && obj.GetHashCode() == GetHashCode();
	}

	public override string ToString() {
		return $"{x};{y}";
	}
}
