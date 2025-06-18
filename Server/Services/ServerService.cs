using Server.Models;
using Server.Models.DTOs;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Timers;
using System.Windows;
using System.Windows.Data;

namespace Server.Services
{
    public class ServerService
    {
        private UdpClient _udpClient;
        private readonly object _lockObj = new object();
        private Dictionary<string, int> _opcionesContador = new();
        private int _port;
        public ObservableCollection<RegistrationDto> RegisteredClients { get; set; } = new ObservableCollection<RegistrationDto>();
        private ObservableCollection<string> _usuariosQueRespondieron = new();
        private Dictionary<string, int> _userScores = new Dictionary<string, int>();

        public event EventHandler<AnswerModel> AnswerReceived;
        public event EventHandler<List<RegistrationDto>> QuizFinished;
        public event EventHandler<Dictionary<string, int>> SendVotos;
        public event EventHandler<Dictionary<string, int>> SendPuntaje;

        private System.Timers.Timer _heartbeatTimer;
        private Dictionary<string, DateTime> _lastHeartbeat = new Dictionary<string, DateTime>();
        private readonly int HEARTBEAT_INTERVAL = 1000;
        private readonly int CLIENT_TIMEOUT = 1000;
        public event EventHandler<string> ClientDisconnected;
        public ServerService(int port)
        {
            _port = port;
            _udpClient = new UdpClient();
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, _port));
            BindingOperations.EnableCollectionSynchronization(RegisteredClients, _lockObj);

            var hilo = new Thread(new ThreadStart(ReceiveAnswersAsync))
            {
                IsBackground = true
            };
            hilo.Start();
           ; 
        }

        private void InitializeHeartbeat()
        {
            _heartbeatTimer = new System.Timers.Timer(HEARTBEAT_INTERVAL);
            _heartbeatTimer.Elapsed += OnHeartbeatTimer;
            _heartbeatTimer.Start();
        }
        private async void OnHeartbeatTimer(object sender, ElapsedEventArgs e)
        {
            await SendHeartbeatToClients();
            CheckDisconnectedClients();
        }
        private async Task SendHeartbeatToClients()
        {
            var heartbeatMessage = new
            {
                Type = "HEARTBEAT",
                Timestamp = DateTime.Now
            };

            var json = JsonSerializer.Serialize(heartbeatMessage);
            var buffer = Encoding.UTF8.GetBytes(json);

            List<RegistrationDto> clientsCopy;
            lock (_lockObj)
            {
                clientsCopy = RegisteredClients.ToList();
            }

            foreach (var client in clientsCopy)
            {
                try
                {
                    var endpoint = new IPEndPoint(IPAddress.Parse(client.IPAddress), 11000);
                    await _udpClient.SendAsync(buffer, buffer.Length, endpoint);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error enviando heartbeat a {client.IPAddress}: {ex.Message}");
                    
                    RemoveClient(client.IPAddress);
                }
            }
        }
        private void CheckDisconnectedClients()
        {
            var now = DateTime.Now;
            var clientsToRemove = new List<string>();

            lock (_lockObj)
            {
                foreach (var client in RegisteredClients.ToList())
                {
                    if (_lastHeartbeat.ContainsKey(client.IPAddress))
                    {
                        var timeSinceLastHeartbeat = now - _lastHeartbeat[client.IPAddress];
                        if (timeSinceLastHeartbeat.TotalMilliseconds > CLIENT_TIMEOUT)
                        {
                            clientsToRemove.Add(client.IPAddress);
                        }
                    }
                    else
                    {
                        
                        _lastHeartbeat[client.IPAddress] = now;
                    }
                }
            }

            foreach (var clientIp in clientsToRemove)
            {
                RemoveClient(clientIp);
            }
        }
        private void RemoveClient(string clientIp)
        {
            lock (_lockObj)
            {
                var clientToRemove = RegisteredClients.FirstOrDefault(c => c.IPAddress == clientIp);
                if (clientToRemove != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        RegisteredClients.Remove(clientToRemove);
                    });

                    _lastHeartbeat.Remove(clientIp);
                    Console.WriteLine($"Cliente desconectado: {clientToRemove.UserName} ({clientIp})");

                   
                    ClientDisconnected?.Invoke(this, clientToRemove.UserName);
                }
            }
        }
        public async Task SendQuestionAsync(QuestionModel question)
        {

            List<RegistrationDto> clientsCopy;
            _usuariosQueRespondieron.Clear();
            
            if (_opcionesContador.Count > 0)
            {
                SendVotos?.Invoke(this, _opcionesContador);
            }
            if (RegisteredClients.Count > 0)
            {
                SendPuntaje?.Invoke(this, _userScores);
            }
            
            _opcionesContador.Clear();
            foreach (var opcion in question.Options)
            {
                if (!_opcionesContador.ContainsKey(opcion))
                    _opcionesContador[opcion] = 0;
            }
            var json = JsonSerializer.Serialize(question);
            var buffer = Encoding.UTF8.GetBytes(json);
            var endpoint = new IPEndPoint(0, 0);
          
            foreach (var item in RegisteredClients)
            {

                endpoint = new IPEndPoint(IPAddress.Parse(item.IPAddress), 11000);
                await _udpClient.SendAsync(buffer, buffer.Length, endpoint);
            }

        }


        private void ReceiveAnswersAsync()
        {
            IPEndPoint? rem = null;
            while (true)
            {
                byte[] result = _udpClient.Receive(ref rem);
                var json = Encoding.UTF8.GetString(result);
                if (json.Contains("HEARTBEAT_RESPONSE"))
                {
                    var heartbeatResponse = JsonSerializer.Deserialize<HearthBeatReponse>(json);
                    _lastHeartbeat[heartbeatResponse.ClientIP] = DateTime.Now;
                    continue;
                }
                if (json.Contains("IPAddress"))
                {
                    var registration = JsonSerializer.Deserialize<RegistrationDto>(json);
                    bool existe = RegisteredClients.Any(x => x.UserName == registration.UserName);
                    if (!existe)
                    {
                        var dto = new RegistrationDto
                        {
                            UserName = registration.UserName,
                            IPAddress = registration.IPAddress,
                            CorrectAnswers = 0
                        };
                        AgregarUsuario(dto);
                        _lastHeartbeat[registration.IPAddress] = DateTime.Now;
                        if (!_userScores.ContainsKey(registration.UserName))
                        {
                            _userScores[registration.UserName] = registration.CorrectAnswers; 
                        }
                        continue;
                    }
                    else
                    {

                        EnviarMensaje("Usuario ya registrado", registration.IPAddress);
                    }
                    continue;
                }

                if (json.Contains("SelectedOption"))
                {

                    var answer = JsonSerializer.Deserialize<AnswerModel>(json);
                    if (!_usuariosQueRespondieron.Contains(answer.UserName))
                    {
                        _usuariosQueRespondieron.Add(answer.UserName);
                        if (_opcionesContador.ContainsKey(answer.SelectedOption))
                        {
                            _opcionesContador[answer.SelectedOption]++;
                        }

                        AnswerReceived?.Invoke(this, answer);
                        EnviarMensaje("Respuesta recibida", answer.IpAdress);
                    }
                }


            }
        }

        public void EnviarMensaje(string v, string? iPAddress)
        {
            var obj = new
            {
                Mensaje = v
            };
            var json = JsonSerializer.Serialize(obj);
            var datos = Encoding.UTF8.GetBytes(json);
            var endpoint = new IPEndPoint(0, 0);
            if (v == "Habilitar Botones")
            {
                foreach (var item in RegisteredClients)
                {

                    endpoint = new IPEndPoint(IPAddress.Parse(item.IPAddress), 11000);
                    _udpClient?.Send(datos, datos.Length, endpoint);
                }
            }
            else
            {
                endpoint = new IPEndPoint(IPAddress.Parse(iPAddress), 11000);
                _udpClient?.Send(datos, datos.Length, endpoint);
            }
        }

        public async Task SendResultsAsync()
        {
           



            QuizFinished?.Invoke(this, RegisteredClients.ToList());
        }


        public void UpdateUserScore(string userName, bool isCorrect)
        {
           
            var usuario = RegisteredClients.FirstOrDefault(u => u.UserName == userName);
            if (usuario != null && isCorrect)
            {
                _userScores[userName]++;
                usuario.CorrectAnswers++;

            }
        }
        public async Task SendScoresClients(string ip, int correctAnswers)
        {
            var obj = new
            {
                CorrectAnswers = correctAnswers
            };
            var json = JsonSerializer.Serialize(obj);
            var buffer = Encoding.UTF8.GetBytes(json);
            var endpoint = new IPEndPoint(IPAddress.Parse(ip), 11000);
            await _udpClient.SendAsync(buffer, buffer.Length, endpoint);
        }
        private void AgregarUsuario(RegistrationDto dto)
        {

            if (!RegisteredClients.Any(c => c.IPAddress == dto.IPAddress))
            {

                Application.Current.Dispatcher.Invoke(() =>
                {
                    RegisteredClients.Add(dto);
                });
            }
        }
    }
}
