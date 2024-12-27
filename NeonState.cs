using System;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;
using I2.Loc;
using System.Linq;
using HarmonyLib;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using System.Runtime.InteropServices;

namespace NeonState
{
    public class KeyPressSim
    {
        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;

        public const int VK_SPACE = 0x20;
        public const int VK_RETURN = 0x0D;
        public const int VK_LEFT = 0x25;
        public const int VK_RIGHT = 0x27;

        public static void SimulateKeyPress(int keyCode)
        {
            IntPtr gameWindow = GetForegroundWindow();
            if (gameWindow != IntPtr.Zero)
            {
                PostMessage(gameWindow, WM_KEYDOWN, keyCode, 0);
                Thread.Sleep(400);
                PostMessage(gameWindow, WM_KEYUP, keyCode, 0);
            }
        }
    }

    public struct CardDeck
    {
        public string Name;
        public int Ammo;
    }

    public class GameState
    {
        public string state { get; set; }
        public bool is_dashing { get; set; }
        public long timer { get; set; }
        public int health { get; set; }
        public bool is_alive { get; set; }
        public CardDeck[] cards { get; set; }
        public int activeCardAmmo { get; set; }
        public int enemies_remaining { get; set; }
        public string activeCard { get; set; }
        public float distance_to_finish { get; set; }
        public Vector3Dto position { get; set; }
        public Vector3Dto direction { get; set; }
        public Vector3Dto velocity { get; set; }
        public string level { get; set; }
        public byte[] pov { get; set; }
    }

    public class Vector3Dto
    {
        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }

        public Vector3Dto(Vector3 v)
        {
            x = v.x;
            y = v.y;
            z = v.z;
        }

        public Vector3Dto() { }
    }

    public class NeonState : MelonMod
    {
        private Socket socket;
        private const int PORT = 42069;
        private bool isConnected = false;
        private Thread receiveThread;
        private bool shouldReceive = true;
        private float lastUpdateTime = 0f;
        private const float UPDATE_INTERVAL = 0.05f; // 50ms interval

        private void InitializeSocket()
        {
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Loopback, PORT);
                socket.Connect(localEndPoint);
                isConnected = true;

                MelonLogger.Msg("Successfully connected to Python script");

                receiveThread = new Thread(ReceiveMessages);
                receiveThread.Start();
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Failed to connect to Python script: {e.Message}");
                isConnected = false;
            }
        }

        private void ReceiveMessages()
        {
            byte[] buffer = new byte[1024];
            while (shouldReceive)
            {
                try
                {
                    if (socket != null && socket.Connected &&
                        RM.mechController &&
                        Game.GetCurrentLevelType() != LevelData.LevelType.Hub)
                    {
                        int received = socket.Receive(buffer);
                        if (received > 0)
                        {
                            string message = Encoding.UTF8.GetString(buffer, 0, received);
                            MelonLogger.Msg($"Received message: {message}");

                            // Simulate space key press when message received
                            //KeyPressSim.SimulateKeyPress(KeyPressSim.VK_SPACE);
                        }
                    }
                }
                catch (Exception e)
                {
                    MelonLogger.Error($"Error receiving message: {e.Message}");
                    isConnected = false;
                    break;
                }
            }
        }

        private void SendGameState(GameState gameState)
        {
            if (!isConnected)
            {
                InitializeSocket();
            }

            try
            {
                string jsonString = JsonConvert.SerializeObject(gameState);
                byte[] messageBytes = Encoding.UTF8.GetBytes(jsonString + "\n");
                socket.Send(messageBytes);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Failed to send game state: {e.Message}");
                isConnected = false;
            }
        }

        public bool is_dashing = false;
        public Vector3 velocity = Vector3.zero;
        public Vector3 pos = Vector3.zero;
        public Vector3 dir = Vector3.zero;
        public long timer = 0;
        public int health = 3;
        public bool is_alive = true;
        public int active_card_ammo = 0;
        public string active_card = "";
        public float distance_to_finish = 0f;
        public bool is_boosting = false;
        public CardDeck[] card_deck;
        public int enemies_remaining = 0;
        public string current_level = "";
        public byte[] player_view = null;

        public static Game Game { get; private set; }

        public override void OnLateInitializeMelon()
        {
            Game = Singleton<Game>.Instance;
            Game.OnLevelLoadComplete += OnLevelLoadComplete;
            InitializeSocket();
        }

        private void OnLevelLoadComplete()
        {
        }

        public byte[] CaptureAndSavepov()
        {
            // Get the main camera
            Camera playerCamera = RM.mechController.playerCamera.PlayerCam;

            // Create a new RenderTexture for the full screen
            RenderTexture rt = new RenderTexture(Screen.width, Screen.height, 24);
            RenderTexture prev = RenderTexture.active;
            playerCamera.targetTexture = rt;
            RenderTexture.active = rt;

            // Render the camera's view
            playerCamera.Render();

            // Create a texture for the center portion
            int size = 200;
            int startX = (Screen.width - size) / 2;
            int startY = (Screen.height - size) / 2;
            Texture2D centerShot = new Texture2D(size, size, TextureFormat.RGB24, false);

            // Read pixels from the center portion
            centerShot.ReadPixels(new Rect(startX, startY, size, size), 0, 0);
            centerShot.Apply();

            // Save raw pixel data in RGB format
            byte[] rawBytes = new byte[size * size * 3];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color pixel = centerShot.GetPixel(x, y);
                    int index = (y * size + x) * 3;
                    rawBytes[index] = (byte)(pixel.r * 255);
                    rawBytes[index + 1] = (byte)(pixel.g * 255);
                    rawBytes[index + 2] = (byte)(pixel.b * 255);
                }
            }

            // Clean up
            playerCamera.targetTexture = null;
            RenderTexture.active = prev;
            UnityEngine.Object.Destroy(rt);
            UnityEngine.Object.Destroy(centerShot);
            return rawBytes;
        }

        public override void OnUpdate()
        {
            string currentSceneName = SceneManager.GetActiveScene().name;
            if (currentSceneName == "Menu")
            {
                SceneManager.LoadScene("Heaven_Environment");
                return;
            }
            if (Game)
            {
                // Only update and send state every UPDATE_INTERVAL seconds
                if (Time.time - lastUpdateTime < UPDATE_INTERVAL)
                {
                    return;
                }
                lastUpdateTime = Time.time;

                this.pos = RM.playerPosition;
                this.dir = RM.mechController.playerCamera.PlayerCam.transform.forward;
                this.velocity = RM.drifter.Motor.BaseVelocity;
                this.is_dashing = RM.drifter.GetIsDashing();
                this.timer = Game.GetCurrentLevelTimerMicroseconds();
                this.health = RM.mechController.currentHealth;
                this.is_alive = RM.mechController.GetIsAlive();
                this.active_card_ammo = RM.mechController.GetCurrentHand()[0].GetCurrentAmmo();
                this.active_card = LocalizationManager.GetTranslation(RM.mechController.GetPlayerCardDeck().GetCardInHand(0).data.cardName).Split(' ')[0];
                this.card_deck = RM.mechController.GetPlayerCardDeck().GetCurrentHand().Select(card => new CardDeck { Name = LocalizationManager.GetTranslation(card.data.cardName).Split(' ')[0], Ammo = card.GetCurrentAmmo() }).ToArray();
                this.enemies_remaining = GameObject.Find("Enemy Encounter").GetComponent<EnemyWave>().GetEnemiesRemaining();
                this.current_level = SceneManager.GetActiveScene().name;
                Vector3 normal_velocity = RM.drifter.Velocity;
                Vector3 move_velocity = RM.drifter.MovementVelocity;
                Bounds triggerMeshBounds = GameObject.Find("Level Goal").transform.Find("Trigger").GetComponent<MeshCollider>().bounds;
                Vector3 finish_location = triggerMeshBounds.center;
                float distanceToFinish = Vector3.Distance(pos, finish_location);
                this.distance_to_finish = distanceToFinish;
                this.player_view = CaptureAndSavepov();

                if (RM.mechController && Game.GetCurrentLevelType() != LevelData.LevelType.Hub)
                {
                    GameState gameState;

                    gameState = new GameState
                    {
                        position = new Vector3Dto(pos),
                        direction = new Vector3Dto(dir),
                        velocity = new Vector3Dto(velocity),
                        is_dashing = is_dashing,
                        timer = timer,
                        health = health,
                        is_alive = is_alive,
                        cards = card_deck,
                        activeCardAmmo = active_card_ammo,
                        activeCard = active_card,
                        distance_to_finish = distance_to_finish,
                        enemies_remaining = enemies_remaining,
                        pov = player_view,
                    };

                    // Send game state through socket
                    SendGameState(gameState);
                }
                else
                {
                    GameState gameState;

                    gameState = new GameState
                    {
                        level = current_level,
                    };

                    // Send game state through socket
                    SendGameState(gameState);

                    bool gamestate_logging = true;

                    if (gamestate_logging == true)
                    {
                        MelonLogger.Msg($"\n" +
                           $"   alive = {gameState.is_alive}\n" +
                           $"   health = {gameState.health}\n" +
                           $"   is_dashing = {gameState.is_dashing}\n" +
                           $"   position = ({gameState.position.x}, {gameState.position.y}, {gameState.position.z})\n" +
                           $"   direction = ({gameState.direction.x}, {gameState.direction.y}, {gameState.direction.z})\n" +
                           $"   velocity = ({gameState.velocity.x}, {gameState.velocity.y}, {gameState.velocity.z})\n" +
                           $"   timer = {gameState.timer}\n" +
                           $"   distanceToFinish = {gameState.distance_to_finish}\n" +
                           $"   level = {gameState.level}\n" +
                           $"   activeCard = {gameState.activeCard}\n" +
                           $"   activeCardAmmo = {gameState.activeCardAmmo}\n" +
                           $"   cards = {string.Join(", ", gameState.cards.Select((card, index) => $"{card.Name} ({card.Ammo})"))}\n" +
                           $"   enemies_remaining = {gameState.enemies_remaining}\n" +
                           $"   pov sending = {gameState.pov.Length} bytes\n"
                            );
                    }

                    //var enemyDict = EnemyWavePatch.GetEnemyDict();

                }
            }
        }

        public override void OnApplicationQuit()
        {
            shouldReceive = false;
            if (receiveThread != null)
            {
                receiveThread.Join();
            }
            if (socket != null)
            {
                socket.Close();
            }
        }
    }
}