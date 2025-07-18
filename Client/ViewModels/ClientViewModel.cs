﻿using Client.Models.DTOs;
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
using System.Timers;
using System.Windows;
using System.Windows.Input;

namespace Client.ViewModels
{
    public class ClientViewModel : INotifyPropertyChanged
    {
        private ClientService _clientService;
        private ClientView _clientView;
        private string _serverIp;
        private string _userName;
        private int _secondsRemaining;


        private System.Timers.Timer _timer;
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
                CanAnswer = true;
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
        private bool _canAnswer = false;
        public bool CanAnswer
        {
            get => _canAnswer;
            set
            {
                _canAnswer = value;
                OnPropertyChanged(nameof(CanAnswer));

            }
        }

        public int SecondsRemaining
        {
            get => _secondsRemaining;
            set
            {
                _secondsRemaining = value;
                OnPropertyChanged(nameof(SecondsRemaining));
            }
        }
        public ICommand ConnectCommand { get; }
        public ICommand SendAnswerCommand { get; }

        private bool RegistrationFailed { get; set; } = false;
        public ClientViewModel()
        {

            ConnectCommand = new RelayCommand(Connect);
            SendAnswerCommand = new RelayCommand<string>(SendAnswer);
            SecondsRemaining = 10;
            _timer = new System.Timers.Timer(1000);
            _timer.Elapsed += OnTimerElapsed;
         
        }

        private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            if (SecondsRemaining > 0)
            {
                SecondsRemaining--;
            }
            else
            {
                _timer.Stop();
            }
            OnPropertyChanged(nameof(SecondsRemaining));
        }

        private void Connect()
        {

            RegistrationFailed = false;
            if (string.IsNullOrWhiteSpace(ServerIp) || string.IsNullOrEmpty(UserName))
            {
                MessageBox.Show("Por favor, ingresa una dirección IP válida y un nombre de usuario");
                return;
            }


            string localIp = GetLocalIpAddress();

            if (_clientService == null) // Solo crear una instancia si no existe
            {
                _clientService = new ClientService(ServerIp, 5001, UserName, localIp);
                _clientService.QuestionReceived += OnQuestionReceived;
                _clientService.ResultReceived += OnResultReceived;
                _clientService.RespuestaReceived += OnRespuestaReceived;
            }
            else
            {

                _clientService.UpdateCredentials(ServerIp, UserName, localIp);
            }

            _clientService.SendRegistration();


            _clientService.MensajeRegistradoReceived += () =>
            {

                MessageBox.Show("El usuario ya se encuentra registrado. Intenta con otro.");
                RegistrationFailed = true;
                UserName = "";
                ServerIp = "";

            };
            if (!RegistrationFailed)
            {

                _clientView = new ClientView();
                _clientView.DataContext = this;
                _clientView.Show();


                Application.Current.MainWindow.Close();
                return;
            }
        }

        private async void OnRespuestaReceived(object? sender, string e)
        {
            if (e == "Habilitar Botones")
            {

                CanAnswer = true;
                await Task.Delay(500);
                _timer.Start();
            }
            else
            {

                MessageBox.Show(e.ToString());
            }
        }

        private void _clientService_RespuestaReceived(object? sender, string e)
        {
            throw new NotImplementedException();
        }

        private void SendAnswer(string resp)
        {

            var answer = new AnswerMessageDTO
            {
                UserName = UserName,
                SelectedOption = resp,
                IpAdress = GetLocalIpAddress(),

            };

            _clientService.SendAnswerAsync(answer);
            CanAnswer = false;
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
            return "127.0.0.1"; 
        }
        private void OnQuestionReceived(object sender, QuestionDto question)
        {
            _timer.Stop();
            CurrentQuestion = question.Question;
            CurrentOptions = question.Options;

            SecondsRemaining = 10;
            OnPropertyChanged(nameof(CurrentQuestion));
            OnPropertyChanged(nameof(CurrentOptions));

            OnPropertyChanged(nameof(SecondsRemaining));
            CanAnswer=false;
        }

        private void OnResultReceived(object sender, ResultDTO result)
        {
            CorrectAnswers = result.CorrectAnswers;
            Application.Current.Dispatcher.Invoke(() =>
            {

                var resultView = new ResultsView();
                resultView.DataContext = this; 
                resultView.Show();
                _clientView?.Close();
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
