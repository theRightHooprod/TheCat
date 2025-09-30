using Godot;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TheCat.Main
{
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

		private async void StartClient()
		{
			startAsServerButton.Hide();
			startAsClientButton.Hide();
			titleScreen.Hide();

			gameStarted = true;

			using var client = new TcpClient();

			try
			{
				await client.ConnectAsync(IPAddress.Loopback, defaultPort);
				WaitingScreen.Hide();
				GD.Print($"Connected to server in port {defaultPort}");

				using NetworkStream stream = client.GetStream();


				byte[] dataToSend = Encoding.UTF8.GetBytes("¡¡HOLA MUNDO!!");
				await stream.WriteAsync(dataToSend, 0, dataToSend.Length);
				GD.Print($"Message sended");


				GD.Print("\nDone. Press any key to exit.");
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
			startAsServerButton.Hide();
			startAsClientButton.Hide();
			titleScreen.Hide();

			var listener = new TcpListener(IPAddress.Any, defaultPort);
			listener.Start();
			GD.Print($"Server listening on port {defaultPort}...");

			while (true)
			{
				TcpClient client = await listener.AcceptTcpClientAsync();
				GD.Print("Client connected.");
				WaitingScreen.Hide();
				gameStarted = true;

				NetworkStream stream = client.GetStream();

				byte[] buffer = new byte[1024];
				int bytesRead;

				try
				{
					while ((bytesRead = await stream.ReadAsync(buffer)) != 0)
					{
						string received = Encoding.UTF8.GetString(buffer, 0, bytesRead);
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

		public override void _Ready()
		{
			startAsServerButton.Pressed += StartServer;
			startAsClientButton.Pressed += StartClient;
		}

		public override void _Input(InputEvent @event)
		{
			if (gameStarted == true && @event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
			{
				var sprite = new Sprite2D();

				string src = isPlayer1 ? "res://circle.png" : "res://cross.png";

				Texture2D texture = GD.Load<Texture2D>(src);
				sprite.Texture = texture;
				sprite.Centered = true;
				sprite.Scale = new Vector2(2, 2);

				sprite.Position = GetViewport().GetMousePosition();

				AddChild(sprite);

			}
		}


	}
}
