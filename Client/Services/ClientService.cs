using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Client.Models.DTOs;

namespace Client.Services
{
    public class ClientService
    {
        private UdpClient _udpClient;
        private string _serverIp;
        private int _port;
        private string _userName;
        private string _clientIp;

        public event EventHandler<QuestionDto> QuestionReceived;
        public event EventHandler<ResultDTO> ResultReceived;
        public event Action MensajeRegistradoReceived;
        public event EventHandler<string> RespuestaReceived;



        public ClientService(string serverIp, int port, string UserName, string ip)
        {
            _serverIp = serverIp;
            _port = port;
            _userName = UserName;
            _clientIp = ip;
            _udpClient = new UdpClient(11000);
            var hilo = new Thread(new ThreadStart(ReceiveMessagesAsync))
            {
                IsBackground = true
            };
            hilo.Start();
            //Task.Run(ReceiveMessagesAsync); 
        }


        public async Task SendAnswerAsync(AnswerMessageDTO answer)
        {
            var json = JsonSerializer.Serialize(answer);
            var buffer = Encoding.UTF8.GetBytes(json);

            var endpoint = new IPEndPoint(IPAddress.Parse(_serverIp), 5000);
            await _udpClient.SendAsync(buffer, buffer.Length, endpoint);
        }
        private bool _isRunning = true;
        public void Cerrar()
        {
            _isRunning = false;
            _udpClient?.Close();
            _udpClient?.Dispose();
        }
        public void SendRegistration()
        {
            var registration = new RegistrationDto
            {
                UserName = _userName,
                IPAddress = _clientIp
            };

            var json = JsonSerializer.Serialize(registration);
            var buffer = Encoding.UTF8.GetBytes(json);
            var endpoint = new IPEndPoint(IPAddress.Parse(_serverIp), 5000);
            _udpClient.Send(buffer, buffer.Length, endpoint);
        }
        public void UpdateCredentials(string serverIp, string userName, string clientIp)
        {
            _serverIp = serverIp;
            _userName = userName;
            _clientIp = clientIp;
        }
        private void ReceiveMessagesAsync()
        {
            while (true)
            {
                try
                {

                    IPEndPoint rem = new IPEndPoint(IPAddress.Any, 0);
                    var result = _udpClient.Receive(ref rem);
                    var json = Encoding.UTF8.GetString(result);


                    if (json.Contains("Question"))
                    {
                        var question = JsonSerializer.Deserialize<QuestionDto>(json);
                        QuestionReceived?.Invoke(this, question);
                    }
                    else if (json.Contains("CorrectAnswers"))
                    {
                        var resultModel = JsonSerializer.Deserialize<ResultDTO>(json);
                        ResultReceived?.Invoke(this, resultModel);
                    }
                    else if (json.Contains("Mensaje"))
                    {
                        if (json.Contains("\"Usuario ya registrado\""))
                        {

                            MensajeRegistradoReceived?.Invoke();
                            json = string.Empty;
                        }
                        else if (json.Contains("\"Respuesta recibida\""))
                        {
                            RespuestaReceived?.Invoke(this, "Respuesta enviada");
                            json = string.Empty;
                        }
                        else if (json.Contains("\"Habilitar Botones\""))
                        {
                            RespuestaReceived?.Invoke(this, "Habilitar Botones");
                            json = string.Empty;
                        }
                    }
                }
                catch (Exception ex)
                {

                    if (!_isRunning)
                        break;
                }

            }
        }
    }
}
