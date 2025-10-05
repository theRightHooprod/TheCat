using Godot;
using System;
using TheCat.Main;

public enum Mark { None, Player1, Player2 }

public struct Cell
{
	public Mark Owner;          // Who claimed the cell
	public CollisionShape2D Shape; // Reference to the shape (so we can disable it)
}

public partial class TicTacToeBoard : Area2D
{
	[Export]
	public Sprite2D localWin;

	[Export]
	public Sprite2D remoteWin;

	[Export]
	public Sprite2D draw;

	[Export] private NodePath _mainScriptPath;   // drag the node in the inspector

	[Export]
	public Button restartGame;

	private Main _mainScript;            // cached reference

	// 3×3 board
	private Cell[,] _board = new Cell[3, 3];

	// Size of each cell (assumes the grid is a square)
	// Adjust to match your actual sprite/shape size.
	private const float CellSize = 64f; // pixels

	// Origin of the grid relative to this Area2D (top‑left corner)
	// If the grid is centered, set this to -(CellSize * 1.5f) for both axes.
	private Vector2 _gridOrigin = new Vector2(-96, -96); // example for 3×3 of 64px cells

	// Game state
	public override void _Ready()
	{
		_mainScript = GetNode<Main>(_mainScriptPath);
		// Find all nine CollisionShape2D children and store them.
		// They should be direct children of this Area2D.
		int index = 0;
		foreach (Node child in GetChildren())
		{
			if (child is CollisionShape2D shape)
			{
				int row = index / 3;
				int col = index % 3;
				_board[row, col] = new Cell { Owner = Mark.None, Shape = shape };
				index++;
				if (index >= 9) break;
			}
		}

		restartGame.Pressed += ResetBoard;

		// Connect the signal that tells us when something entered the area.
		Connect("area_entered", new Callable(this, nameof(OnAreaEntered)));
	}

	private void OnAreaEntered(Area2D other)
	{
		if (!_mainScript.gameStarted) return;
		if (other == null) return;

		// Determine which player the entering object belongs to.
		Mark playerMark;
		if (other.IsInGroup("Player1")) playerMark = Mark.Player1;
		else if (other.IsInGroup("Player2")) playerMark = Mark.Player2;
		else
		{
			GD.Print($"Ignored object not in Player1/Player2 groups: {other.Name}");
			return;
		}

		// ------------------------------------------------------------
		// Find the world position where the object intersected the grid.
		// ------------------------------------------------------------
		// We use the object's global position and transform it into the
		// local space of this Area2D (the grid).
		Vector2 localPos = ToLocal(other.GlobalPosition);

		// ------------------------------------------------------------
		// 2️ Map that local position to a row/column.
		// ------------------------------------------------------------
		int col = (int)Math.Floor((localPos.X - _gridOrigin.X) / CellSize);
		int row = (int)Math.Floor((localPos.Y - _gridOrigin.Y) / CellSize);

		// Clamp to valid range – if the calculation lands outside the board,
		// just ignore the move.
		if (row < 0 || row > 2 || col < 0 || col > 2)
		{
			GD.Print($"Click outside grid: ({row},{col})");
			return;
		}

		// ------------------------------------------------------------
		// Apply the move if the cell is empty.
		// ------------------------------------------------------------
		ref Cell targetCell = ref _board[row, col];
		if (targetCell.Owner != Mark.None)
		{
			GD.Print($"Cell ({row},{col}) already taken.");
			return;
		}

		targetCell.Owner = playerMark;
		// Optionally disable the shape so nothing else can trigger it.
		targetCell.Shape.SetDeferred("disabled", true);

		GD.Print($"Player {(playerMark == Mark.Player1 ? "1" : "2")} placed at ({row},{col})");

		// ------------------------------------------------------------
		// Check for win / draw.
		// ------------------------------------------------------------
		if (CheckWin(playerMark))
		{
			_mainScript.gameStarted = false;
			GD.Print($"Player {(playerMark == Mark.Player1 ? "1" : "2")} wins!");
			if (playerMark == Mark.Player1)
			{
				localWin.Visible = true;
				restartGame.Visible = true;
			}
			else
			{
				remoteWin.Visible = true;
				restartGame.Visible = true;

			}
			// You could emit a signal here to update UI, play sound, etc.
		}
		else if (IsBoardFull())
		{
			_mainScript.gameStarted = false;
			GD.Print("Draw – board is full.");

			draw.Visible = true;
			restartGame.Visible = true;

		}
	}

	// ------------------------------------------------------------------------
	// Win detection – scans rows, columns and diagonals for three equal marks.
	// ------------------------------------------------------------------------
	private bool CheckWin(Mark mark)
	{
		// rows & columns
		for (int i = 0; i < 3; i++)
		{
			if (_board[i, 0].Owner == mark &&
				_board[i, 1].Owner == mark &&
				_board[i, 2].Owner == mark) return true; // row i

			if (_board[0, i].Owner == mark &&
				_board[1, i].Owner == mark &&
				_board[2, i].Owner == mark) return true; // column i
		}

		// diagonals
		if (_board[0, 0].Owner == mark &&
			_board[1, 1].Owner == mark &&
			_board[2, 2].Owner == mark) return true;

		if (_board[0, 2].Owner == mark &&
			_board[1, 1].Owner == mark &&
			_board[2, 0].Owner == mark) return true;

		return false;
	}

	private bool IsBoardFull()
	{
		for (int r = 0; r < 3; r++)
			for (int c = 0; c < 3; c++)
				if (_board[r, c].Owner == Mark.None)
					return false;
		return true;
	}

	// ------------------------------------------------------------------------
	// Public helper – call this from a UI button to start a new round.
	// ------------------------------------------------------------------------
	public void ResetBoard()
	{
		_mainScript.GetTree().CallGroup("texture", "queue_free"); // clean the items
		_mainScript.GetTree().CallGroup("Player1", "queue_free"); // clean the items
		_mainScript.GetTree().CallGroup("Player2", "queue_free"); // clean the items

		localWin.Visible = false;
		remoteWin.Visible = false;
		draw.Visible = false;

		restartGame.Visible = false;

		for (int r = 0; r < 3; r++)
		{
			for (int c = 0; c < 3; c++)
			{
				_board[r, c].Owner = Mark.None;
				_board[r, c].Shape.SetDeferred("disabled", false);
			}
		}

		_mainScript.gameStarted = true;
		GD.Print("Board reset – new game started.");
	}
}