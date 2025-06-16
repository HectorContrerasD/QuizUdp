using Server.Models;
using Server.Models.DTOs;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace Server.Services
{
    public class ServerService
    {
        private UdpClient _udpClient;
        private int _port;
        public List<RegistrationDto> RegisteredClients { get; set; } = new List<RegistrationDto>();
        private Dictionary<string, int> _userScores = new Dictionary<string, int>();

        public event EventHandler<AnswerModel> AnswerReceived;
        public event EventHandler QuizFinished;
        private readonly object _lock = new object();
        public ServerService(int port)
        {
            _port = port;
            _udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, port));
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



                if (json.Contains("IPAddress"))
                {
                    var registration = JsonSerializer.Deserialize<RegistrationDto>(json);

                    var dto = new RegistrationDto
                    {
                        UserName = registration.UserName,
                        IPAddress = registration.IPAddress,
                    };



                    // Actualiza desde el hilo UI
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
            foreach (var userScore in _userScores)
            {
                var result = new UserScoreModel
                {
                    UserName = userScore.Key,
                    CorrectAnswers = userScore.Value
                };

                var json = JsonSerializer.Serialize(result);
                var buffer = Encoding.UTF8.GetBytes(json);


                var endpoint = new IPEndPoint(IPAddress.Broadcast, _port);
                await _udpClient.SendAsync(buffer, buffer.Length, endpoint);
            }


            QuizFinished?.Invoke(this, EventArgs.Empty);
        }


        public void UpdateUserScore(string userName, bool isCorrect)
        {
            if (_userScores.ContainsKey(userName))
            {
                if (isCorrect)
                {
                    _userScores[userName]++;
                }
            }
            else
            {
                _userScores[userName] = isCorrect ? 1 : 0;
            }
        }
        private void AgregarUsuario(RegistrationDto dto)
        {
            // Evita duplicados si es necesario
            if (!RegisteredClients.Any(c => c.IPAddress == dto.IPAddress))
            {
                RegisteredClients.Add(dto);
            }

        }
    }
}
