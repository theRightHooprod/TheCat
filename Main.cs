#nullable enable

using Godot;
using System;
using System.Collections.Concurrent;
using System.IO;
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
		public required Sprite2D WaitingScreen;

		[Export]
		public required Sprite2D titleScreen;

		[Export]
		public int defaultPort = 7800;

		[Export]
		public required Button startAsServerButton;

		[Export]
		public required Button startAsClientButton;

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

		// Conexiones aceptadas por el listener (servidor)
		private readonly ConcurrentDictionary<string, NetworkStream> inboundStreams = new();

		private void ConnectToPeer(string host, int port)
		{
			try
			{
				await client.ConnectAsync(IPAddress.Loopback, defaultPort);
				GD.Print($"Connected to server in port {defaultPort}");
				StartGame();

				StartGame();

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
				GD.Print($"[Outbound] No se pudo conectar a {host}:{port} – {ex.Message}");
			}
		}

		private void HandleIncoming(string sourceKey, NetworkStream ns, TcpClient client)
		{
			StartWaiting();

			StartGame();

			using var reader = new StreamReader(ns, Encoding.UTF8);
			try
			{
				TcpClient client = await listener.AcceptTcpClientAsync();
				GD.Print("Client connected.");
				StartGame();

					// Reenviar a todos los demás peers (excepto al origen)
					// Broadcast(sourceKey, line);
					SpawnFromString(line);

					Broadcast(sourceKey, line);
				}
			}
			catch (IOException) { /* conexión perdida */ }
			finally
			{
				GD.Print($"[Inbound] Desconectado {sourceKey}");
				inboundStreams.TryRemove(sourceKey, out _);
				client.Close();
			}
		}

		private void Broadcast(string sourceKey, string message)
		{
			byte[] data = Encoding.UTF8.GetBytes(message + "\n");
			// Helper interno para escribir de forma segura en un stream
			void WriteSafe(NetworkStream ns, string targetKey)
			{
				try
				{
					while ((bytesRead = await stream.ReadAsync(buffer)) != 0)
					{
						string received = Encoding.UTF8.GetString(buffer, 0, bytesRead);

						SpawnFromString(received);

						GD.Print($"Received: {received}");
					}
				}
				catch (Exception ex)
				{
					GD.Print($"[Broadcast] Error enviando a {targetKey}: {ex.Message}");
					// Si falla, quitamos el stream problemático
					inboundStreams.TryRemove(targetKey, out _);
					outboundStreams.TryRemove(targetKey, out _);
				}
			}

			// Envío a todas las conexiones salientes
			foreach (var kvp in outboundStreams)
			{
				if (kvp.Key == sourceKey) continue; // no devolver al remitente
				WriteSafe(kvp.Value, kvp.Key);
			}

			// Envío a todas las conexiones entrantes
			foreach (var kvp in inboundStreams)
			{
				if (kvp.Key == sourceKey) continue;
				WriteSafe(kvp.Value, kvp.Key);
			}
		}

		private void SpawnFromString(string data)
		{
			var jsonObj = JsonSerializer.Deserialize<Sprite2DData>(data);
			var obj = CreateFigure(new Vector2(jsonObj.x, jsonObj.y));
			AddChild(obj);
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
