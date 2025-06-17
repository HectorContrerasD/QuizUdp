using Server.Models;
using Server.Models.DTOs;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;

namespace Server.Services
{
    public class ServerService
    {
        private UdpClient _udpClient;
        private readonly object _lockObj = new object();
        private int _port;
        public ObservableCollection<RegistrationDto> RegisteredClients { get; set; } = new ObservableCollection<RegistrationDto>();
        private Dictionary<string, int> _userScores = new Dictionary<string, int>();

        public event EventHandler<AnswerModel> AnswerReceived;
        public event EventHandler<List<RegistrationDto>> QuizFinished;

        public ServerService(int port)
        {
            _port = port;
            _udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, _port));
            BindingOperations.EnableCollectionSynchronization(RegisteredClients, _lockObj);
            var hilo = new Thread(new ThreadStart(ReceiveAnswersAsync))
            {
                IsBackground = true
            };
            hilo.Start();
            //hilo.Join();
            //Task.Run(ReceiveAnswersAsync); 
        }


        public async Task SendQuestionAsync(QuestionModel question)
        {

            List<RegistrationDto> clientsCopy;

            var json = JsonSerializer.Serialize(question);
            var buffer = Encoding.UTF8.GetBytes(json);
            var endpoint = new IPEndPoint(0, 0);
            //lock (_lockObj)
            //{
            //    clientsCopy = RegisteredClients.ToList();
            //}

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



                if (json.Contains("IPAddress"))
                {
                    var registration = JsonSerializer.Deserialize<RegistrationDto>(json);

                    var dto = new RegistrationDto
                    {
                        UserName = registration.UserName,
                        IPAddress = registration.IPAddress,
                    };
                    dto.CorrectAnswers = 0;
                    AgregarUsuario(dto);
                    continue;
                }

                // Manejar respuestas (código existente)
                if (json.Contains("SelectedOption"))
                {
                    var answer = JsonSerializer.Deserialize<AnswerModel>(json);
                    AnswerReceived?.Invoke(this, answer);
                }
            }
        }


        public async Task SendResultsAsync()
        {
            //foreach (var userScore in _userScores)
            //{
            //    var result = new UserScoreModel
            //    {
            //        UserName = userScore.Key,
            //        CorrectAnswers = userScore.Value
            //    };

            //    var json = JsonSerializer.Serialize(result);
            //    var buffer = Encoding.UTF8.GetBytes(json);


            //    var endpoint = new IPEndPoint(IPAddress.Broadcast, _port);
            //    await _udpClient.SendAsync(buffer, buffer.Length, endpoint);
            //}

            //var clients = RegisteredClients;
            QuizFinished?.Invoke(this, RegisteredClients.ToList());
        }


        public void UpdateUserScore(string userName, bool isCorrect)
        {
            //if (_userScores.ContainsKey(userName))
            //{
            //    if (isCorrect)
            //    {
            //        _userScores[userName]++;
            //    }
            //}
            //else
            //{
            //    _userScores[userName] = isCorrect ? 1 : 0;
            //}
            var usuario = RegisteredClients.FirstOrDefault(u => u.UserName == userName);
            if (usuario != null && isCorrect)
            {

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
