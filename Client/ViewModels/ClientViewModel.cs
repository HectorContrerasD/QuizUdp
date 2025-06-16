using Client.Models.DTOs;
using Client.Services;
using Client.Views;
using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Client.ViewModels
{
    public class ClientViewModel
    {
        private ClientService _clientService;
        private string _serverIp;
        private string _userName;

        private string _currentQuestion;
        private string[] _currentOptions;
        private int _correctAnswers;
        public string UserName
        {
            get => _userName;
            set
            {
                _userName = value;
                OnPropertyChanged(nameof(UserName));
            }
        }
        public string ServerIp
        {
            get => _serverIp;
            set
            {
                _serverIp = value;
                OnPropertyChanged(nameof(ServerIp));
            }
        }

        public string CurrentQuestion
        {
            get => _currentQuestion;
            set
            {
                _currentQuestion = value;
                OnPropertyChanged(nameof(CurrentQuestion));
            }
        }

        public string[] CurrentOptions
        {
            get => _currentOptions;
            set
            {
                _currentOptions = value;
                OnPropertyChanged(nameof(CurrentOptions));
            }
        }

        public int CorrectAnswers
        {
            get => _correctAnswers;
            set
            {
                _correctAnswers = value;
                OnPropertyChanged(nameof(CorrectAnswers));
            }
        }

        public ICommand ConnectCommand { get; }
        public ICommand SendAnswerCommand { get; }


        public ClientViewModel()
        {
            
            ConnectCommand = new RelayCommand(Connect);
            SendAnswerCommand = new RelayCommand(SendAnswer);
         
            //_clientService.QuestionReceived += OnQuestionReceived;
            //_clientService.ResultReceived += OnResultReceived;
        }

        private void Connect()
        {

            
            if (string.IsNullOrWhiteSpace(ServerIp))
            {
                MessageBox.Show("Por favor, ingresa una dirección IP válida.");
                return;
            }


            string localIp = GetLocalIpAddress();

            _clientService = new ClientService(ServerIp, 5001, UserName, localIp);
            _clientService.QuestionReceived += OnQuestionReceived;
            _clientService.ResultReceived += OnResultReceived;

            // Enviar registro al servidor
            _clientService.SendRegistration();

            var clientView = new ClientView()
            {
                DataContext = this // Corregido: usar el ViewModel actual
            };
            
            clientView.Show();

            
            Application.Current.MainWindow.Close();
        }

        private void SendAnswer()
        {
          
            var answer = new AnswerMessageDTO
            {
                UserName = UserName,
                SelectedOption = "Alola" 
            };

            _clientService.SendAnswerAsync(answer);
        }
        private string GetLocalIpAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            return "127.0.0.1"; // Fallback a localhost
        }
        private void OnQuestionReceived(object sender, QuestionDto question)
        {

            CurrentQuestion = question.Question;
            CurrentOptions = question.Options;

            OnPropertyChanged(nameof(CurrentQuestion));
            OnPropertyChanged(nameof(CurrentOptions));
        }

        private void OnResultReceived(object sender, ResultDTO result)
        {
           
            CorrectAnswers = result.CorrectAnswers;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName=null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
