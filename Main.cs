#nullable enable

using Godot;
using System;
using System.Collections.Concurrent;
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
		public float X { get; set; }
		public float Y { get; set; }
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

		public Boolean isPlayer1 = true;
		public Boolean gameStarted { get; set; } = false;

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

							SpawnFromString(line);

							Broadcast(key, line);
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
			Thread listenerThread = new(() => StartListener(7801));
			listenerThread.IsBackground = true;
			listenerThread.Start();

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

		private void SpawnFromString(string data)
		{
			var jsonObj = JsonSerializer.Deserialize<Sprite2DData>(data);
			var obj = CreateFigure(new Vector2(jsonObj!.X, jsonObj!.Y));
			CallDeferred("add_child", obj);
		}

		private Sprite2D CreateFigure(Vector2 position, bool isLocal = false)
		{
			var obj = new Sprite2D();

			string src = isLocal ? isPlayer1 ? "res://circle.png" : "res://cross.png" : !isPlayer1 ? "res://circle.png" : "res://cross.png";

			Texture2D texture = GD.Load<Texture2D>(src);

			obj.Texture = texture;

			obj.Centered = true;
			obj.Scale = new Vector2(2, 2);

			obj.Position = position;

			obj.AddToGroup("texture");  //Sets the player type.

			Area2D body = new();

			CollisionShape2D collition = new()
			{
				Shape = new RectangleShape2D { Size = new Vector2(20, 20) }
			};

			body.AddToGroup(isLocal ? "Player1":"Player2");  //Sets the player type.

			body.AddChild(collition);

			obj.AddChild(body);

			GD.Print($"Added group: {collition.GetGroups()}");

			return obj;
		}

		private string ConvertToBytes(Sprite2D obj)
		{
			Sprite2DData dto = new()
			{
				X = obj.Position.X,
				Y = obj.Position.Y,
			};

			return JsonSerializer.Serialize(dto);
		}

		public override void _Input(InputEvent @event)
		{
			if (gameStarted == true && @event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
			{
				Sprite2D shape = CreateFigure(GetViewport().GetMousePosition(), true);
				AddChild(shape);
				string bytes = ConvertToBytes(shape);

				Broadcast("[local]", bytes);

			}

		}


	}
}
