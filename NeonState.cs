using System;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;
using I2.Loc;
using System.Linq;
using HarmonyLib;
using System.Collections.Generic;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using System.Runtime.InteropServices;
using System.IO;
using UnityEngine.InputSystem;

namespace NeonState
{
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
        private const float UPDATE_INTERVAL = 0.025f; // 25ms interval
        private GameState lastGameState = null;


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
                MelonLogger.Error("Terminating");
                Application.Quit();
            }
        }

        private void ReceiveMessages()
        {
            byte[] buffer = new byte[1024];
            while (shouldReceive)
            {
                try
                {
                    if (socket != null && socket.Connected)
                    {
                        int received = socket.Receive(buffer);
                        if (received > 0)
                        {
                            string message = Encoding.UTF8.GetString(buffer, 0, received);
                            MelonLogger.Msg($"Received message: {message}");
                        }
                    }
                }
                catch (Exception e)
                {
                    MelonLogger.Error($"Error receiving message: {e.Message}");
                    isConnected = false;
                    Application.Quit();
                    break;
                }
            }
        }

        private void SendGameState(GameState gameState)
        {
            if (!isConnected)
            {
                InitializeSocket();
                if (!isConnected)
                {
                    Application.Quit();
                    return;
                }
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
                Application.Quit();
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
            InitializeSocket();
        }

        public override void OnUpdate()
        {
            if (Keyboard.current.kKey.wasPressedThisFrame)
            {
                MelonLogger.Msg("Killed");
                Application.Quit();
                return;
            }

            string currentSceneName = SceneManager.GetActiveScene().name;
            if (currentSceneName == "Menu")
            {
                SceneManager.LoadScene("Heaven_Environment");
                return;
            }

            if (Game && RM.mechController && Game.GetCurrentLevelType() != LevelData.LevelType.Hub)
            {
                // Only update and send state every UPDATE_INTERVAL seconds
                if (Time.time - lastUpdateTime < UPDATE_INTERVAL)
                {
                    return;
                }
                lastUpdateTime = Time.time;



                this.is_alive = RM.mechController.GetIsAlive();
                if (this.is_alive){
                    
                    this.pos = RM.playerPosition;
                    this.dir = RM.mechController.playerCamera.PlayerCam.transform.forward;
                    this.velocity = RM.drifter.Motor.BaseVelocity;
                    this.is_dashing = RM.drifter.GetIsDashing();
                    this.timer = Game.GetCurrentLevelTimerMicroseconds();
                    this.health = RM.mechController.currentHealth;
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
                }
            }
            GameState gameState = new GameState
            {
                position = new Vector3Dto(pos),
                direction = new Vector3Dto(dir),
                velocity = new Vector3Dto(velocity),
                is_dashing = is_dashing,
                timer = timer,
                health = health,
                is_alive = is_alive,
                activeCard = active_card,
                distance_to_finish = distance_to_finish,
                enemies_remaining = enemies_remaining,
                pov = player_view,
            };
            // Send game state through socket
            SendGameState(gameState);
            

            MelonLogger.Msg($"\n" +
                    $"   alive = {gameState.is_alive}\n" +
                    $"   health = {gameState.health}\n" +
                    $"   is_dashing = {gameState.is_dashing}\n" +
                    $"   position = ({gameState.position.x}, {gameState.position.y}, {gameState.position.z})\n" +
                    $"   direction = ({gameState.direction.x}, {gameState.direction.y}, {gameState.direction.z})\n" +
                    $"   velocity = ({gameState.velocity.x}, {gameState.velocity.y}, {gameState.velocity.z})\n" +
                    $"   timer = {gameState.timer}\n" +
                    $"   distanceToFinish = {gameState.distance_to_finish}\n" +
                    $"   enemies_remaining = {gameState.enemies_remaining}\n" +
                    $"   pov sending = {gameState.pov.Length} bytes\n"
                );
        }
    

        


        public byte[] CaptureAndSavepov()
        {
            // Get the main camera
            Camera playerCamera = RM.mechController.playerCamera.PlayerCam;

            // Create a RenderTexture at full resolution
            int fullWidth = Screen.width;
            int fullHeight = Screen.height;
            RenderTexture rt = new RenderTexture(fullWidth, fullHeight, 0);
            RenderTexture prev = RenderTexture.active;
            playerCamera.targetTexture = rt;
            RenderTexture.active = rt;

            // Render the camera's view
            playerCamera.Render();

            // Calculate center crop coordinates
            int size = 96;
            int x = (fullWidth - size) / 2;
            int y = (fullHeight - size) / 2;

            // Create texture and read only the center pixels
            Texture2D centerShot = new Texture2D(size, size, TextureFormat.RGB24, false);
            centerShot.ReadPixels(new Rect(x, y, size, size), 0, 0);
            centerShot.Apply();

            // Convert RGB to grayscale
            byte[] rgbBytes = centerShot.GetRawTextureData();
            byte[] grayscale = new byte[size * size];
            for (int i = 0; i < size * size; i++)
            {
                int rgbIndex = i * 3;
                // Standard grayscale conversion weights: R:0.299, G:0.587, B:0.114
                grayscale[i] = (byte)(
                    rgbBytes[rgbIndex] * 0.299f + 
                    rgbBytes[rgbIndex + 1] * 0.587f + 
                    rgbBytes[rgbIndex + 2] * 0.114f
                );
            }

            // Clean up
            playerCamera.targetTexture = null;
            RenderTexture.active = prev;
            UnityEngine.Object.Destroy(rt);
            UnityEngine.Object.Destroy(centerShot);
            
            return grayscale;
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
