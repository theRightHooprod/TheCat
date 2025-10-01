using Godot;
using System;
using System.Collections.Concurrent;
using System.Data.SqlTypes;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;

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
		public Sprite2D WaitingScreen;

		[Export]
		public Sprite2D titleScreen;

		[Export]
		public int defaultPort = 7800;

		[Export]
		public Button startAsServerButton;

		[Export]
		public Button startAsClientButton;

		public Boolean isPlayer1 = true;
		public Boolean gameStarted = false;

		// Conexiones iniciadas por este proceso (cliente)
		private readonly ConcurrentDictionary<string, NetworkStream> outboundStreams = new();

		// Conexiones aceptadas por el listener (servidor)
		private readonly ConcurrentDictionary<string, NetworkStream> inboundStreams = new();

		private void ConnectToPeer(string host, int port)
		{
			try
			{
				TcpClient client = new(host, port);
				NetworkStream ns = client.GetStream();
				string key = $"{host}:{port}";

				outboundStreams[key] = ns;
				GD.Print($"[Outbound] Conectado a {key}");

				StartGame();

				// Hilo que lee datos provenientes de este peer
				Thread t = new(() =>
				{
					using var reader = new StreamReader(ns, Encoding.UTF8);
					try
					{
						while (true)
						{
							string? line = reader.ReadLine();
							if (line == null) break;

							GD.Print($"[From {key}] {line}");

							Sprite2DData incommingObject = JsonSerializer.Deserialize<Sprite2DData>(line);

							Sprite2D sprite = ConvertToObject(incommingObject);

							GetParent().CallDeferred("add_child", sprite);

							byte[] data = ConvertToBytesFromString(line);
							Broadcast(key, data);
						}
					}
					catch (IOException) { /* ignorar */ }
					finally
					{
						GD.Print($"[Outbound] Desconectado de {key}");
						outboundStreams.TryRemove(key, out _);
						client.Close();
					}
				});
				t.IsBackground = true;
				t.Start();
			}
			catch (Exception ex)
			{
				GD.Print($"[Outbound] No se pudo conectar a {host}:{port} – {ex.Message}");
			}
		}

		private void HandleIncoming(string sourceKey, NetworkStream ns, TcpClient client)
		{
			GD.Print($"[Inbound] Conexión aceptada desde {sourceKey}");

			StartGame();

			using var reader = new StreamReader(ns, Encoding.UTF8);
			try
			{
				while (true)
				{
					string? line = reader.ReadLine(); // bloquea hasta recibir '\n'
					if (line == null) break;         // la otra punta cerró la conexión

					// Mostrar en pantalla
					GD.Print($"[{sourceKey}] {line}");

					// Reenviar a todos los demás peers (excepto al origen)
					// Broadcast(sourceKey, line);
					Sprite2DData incommingObject = JsonSerializer.Deserialize<Sprite2DData>(line);

					Sprite2D sprite = ConvertToObject(incommingObject);

					GD.Print("added child");
					GetParent().CallDeferred("add_child", sprite);

					byte[] data = ConvertToBytesFromString(line);
					Broadcast(sourceKey, data);
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

		private void Broadcast(string sourceKey, byte[] data)
		{
			// Helper interno para escribir de forma segura en un stream
			void WriteSafe(NetworkStream ns, string targetKey)
			{
				try
				{
					ns.Write(data, 0, data.Length);
					ns.Flush();
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

		private void StartListener(int port)
		{
			TcpListener listener = new(IPAddress.Any, port);
			GD.Print($"[Listener] Iniciado en puerto {port}");
			StartWaiting();
			listener.Start();
			while (true)
			{
				try
				{
					TcpClient client = listener.AcceptTcpClient(); // bloqueo hasta que llegue una conexión
					string key = client.Client.RemoteEndPoint?.ToString() ?? Guid.NewGuid().ToString();
					NetworkStream ns = client.GetStream();

					// Guardamos el stream para poder reenviarle mensajes más tarde
					inboundStreams[key] = ns;

					// Cada conexión entrante tiene su propio hilo de lectura
					Thread t = new(() => HandleIncoming(key, ns, client));
					t.IsBackground = true;
					t.Start();
				}
				catch (Exception ex)
				{
					GD.Print($"[Listener] Excepción: {ex.Message}");
					break; // Salir del bucle si algo fatal ocurre
				}
			}
		}

		public void StartWaiting()
		{
			startAsServerButton.CallDeferred("hide");
			startAsClientButton.CallDeferred("hide");
			titleScreen.CallDeferred("hide");
		}

		public void StartGame()
		{
			WaitingScreen.CallDeferred("hide");
			gameStarted = true;
		}

		private void StartClient()
		{
			ConnectToPeer(IPAddress.Loopback.ToString(), defaultPort);
			isPlayer1 = false;
			StartWaiting();
		}

		private void StartServer()
		{
			Thread listenerThread = new(() => StartListener(defaultPort));
			listenerThread.IsBackground = true;
			listenerThread.Start();
		}


		public override void _Ready()
		{
			startAsServerButton.Pressed += StartServer;
			startAsClientButton.Pressed += StartClient;
		}

		public byte[] ConvertSpriteToBytes(Sprite2D sprite)
		{
			Sprite2DData data = new Sprite2DData
			{
				x = sprite.Position.X,
				y = sprite.Position.Y,
				texturePath = sprite.Texture.ResourcePath
			};

			string json = JsonSerializer.Serialize(data);

			byte[] bytes = Encoding.UTF8.GetBytes(json);
			return bytes;
		}


		public Sprite2D ConvertToObject(Sprite2DData data)
		{
			var sprite = new Sprite2D();
			Texture2D texture = GD.Load<Texture2D>(data.texturePath);
			sprite.Centered = true;
			sprite.Scale = new Vector2(2, 2);
			sprite.Position = new Vector2(data.x, data.y);

			return sprite;
		}

		public byte[] ConvertToBytesFromString(string data)
		{
			Sprite2DData incommingObject = JsonSerializer.Deserialize<Sprite2DData>(data);
			Sprite2D convertedObject = ConvertToObject(incommingObject);
			return ConvertSpriteToBytes(convertedObject);
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
				byte[] bytes = ConvertSpriteToBytes(sprite);

				Broadcast("[local]", bytes);

			}

		}


	}
}
