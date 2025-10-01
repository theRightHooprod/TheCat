using Godot;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TheCat.Main
{
	public class Sprite2DData
	{
		public float x;
		public float y;
		public string texturePath;
	}
	public partial class Main : Node2D
	{
		[Export]
		public Boolean isPlayer1 = true;

		[Export]
		public Sprite2D WaitingScreen;

		[Export]
		public Sprite2D titleScreen;

		[Export]
		public int defaultPort = 7800;

		[Export]
		public Button startAsServerButton;

		[Export]
		public Button startAsClientButton;

		public Boolean gameStarted = false;

		public override void _Ready()
		{
			startAsServerButton.Pressed += StartServer;
			startAsClientButton.Pressed += StartClient;
		}

		private void StartWaiting()
		{
			startAsServerButton.Hide();
			startAsClientButton.Hide();
			titleScreen.Hide();
		}

		private void StartGame()
		{
			WaitingScreen.Hide();
			gameStarted = true;
		}

		private async void StartClient()
		{
			StartWaiting();

			using var client = new TcpClient();

			try
			{
				await client.ConnectAsync(IPAddress.Loopback, defaultPort);
				GD.Print($"Connected to server in port {defaultPort}");
				StartGame();

				using NetworkStream stream = client.GetStream();


				byte[] dataToSend = ConvertToBytes(CreateFigure(new Vector2(1, 2)));
				await stream.WriteAsync(dataToSend, 0, dataToSend.Length);
				GD.Print($"Message sended");
			}
			catch (SocketException se)
			{
				GD.Print($"Socket error: {se.Message}");
			}
			catch (Exception ex)
			{
				GD.Print($"Unexpected error: {ex.Message}");
			}

			Console.WriteLine("Disconnected.");
		}

		private async void StartServer()
		{
			StartWaiting();

			var listener = new TcpListener(IPAddress.Any, defaultPort);
			listener.Start();
			GD.Print($"Server listening on port {defaultPort}...");

			while (true)
			{
				TcpClient client = await listener.AcceptTcpClientAsync();
				GD.Print("Client connected.");
				StartGame();

				NetworkStream stream = client.GetStream();

				byte[] buffer = new byte[1024];
				int bytesRead;

				try
				{
					while ((bytesRead = await stream.ReadAsync(buffer)) != 0)
					{
						string received = Encoding.UTF8.GetString(buffer, 0, bytesRead);
						var jsonObj = JsonSerializer.Deserialize<Sprite2DData>(received);
						var obj = CreateFigure(new Vector2(jsonObj.x, jsonObj.y));
						AddChild(obj);
						GD.Print($"Received: {received}");
					}
				}
				catch (Exception ex)
				{
					GD.Print($"Error: {ex.Message}");
				}

				GD.Print("Client disconnected.");
			}
		}

		private Sprite2D CreateFigure(Vector2 position)
		{
			var obj = new Sprite2D();

			string src = isPlayer1 ? "res://circle.png" : "res://cross.png";

			Texture2D texture = GD.Load<Texture2D>(src);

			obj.Texture = texture;

			obj.Centered = true;
			obj.Scale = new Vector2(2, 2);

			obj.Position = position;

			return obj;
		}

		private static Byte[] ConvertToBytes(Sprite2D obj)
		{
			Sprite2DData dto = new()
			{
				x = obj.Position.X,
				y = obj.Position.Y,
			};

			string json = JsonSerializer.Serialize(dto);
			return Encoding.UTF8.GetBytes(json);
		}

		public override void _Input(InputEvent @event)
		{
			if (gameStarted == true && @event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
			{
				Sprite2D obj = CreateFigure(GetViewport().GetMousePosition());
				AddChild(obj);
			}
		}


	}
}
