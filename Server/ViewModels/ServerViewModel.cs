using GalaSoft.MvvmLight.Command;
using Newtonsoft.Json;
using Server.Models;
using Server.Models.DTOs;
using Server.Services;
using Server.Views;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Timers;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;


namespace Server.ViewModels
{
    public class ServerViewModel : INotifyPropertyChanged
    {

        private ServerService _serverService;
        private List<QuestionModel> _questions = new();
        private Dictionary<string, int> _votosPorOpcion = new();

        private int _currentQuestionIndex;
        private int _secondsRemaining;
        private System.Timers.Timer _timer;
        private System.Timers.Timer _timerEnabled;
        private System.Timers.Timer _timerResults;

        private Dictionary<string, int> _userScores = new Dictionary<string, int>();

        public string IpAddress { get; }
        public int Port { get; } = 5000;
        private ResultsView? _resultsView;
        private ServerView? _serverView;

        public string CurrentQuestion => _questions[_currentQuestionIndex].Question;
        public string[] CurrentOptions => _questions[_currentQuestionIndex].Options;
        public string CorrectAnswer => _questions[_currentQuestionIndex].CorrectAnswer;

        public int SecondsRemaining
        {
            get => _secondsRemaining;
            set
            {
                _secondsRemaining = value;
                OnPropertyChanged(nameof(SecondsRemaining));
            }
        }
        public Dictionary<string, int> UserScore
        {
            get => _userScores;
            set
            {
                _userScores = value;
                OnPropertyChanged(nameof(UserScore));
            }
        }
        public Dictionary<string, int> VotosPorOpcion
        {
            get => _votosPorOpcion;
            set
            {
                _votosPorOpcion = value;
                OnPropertyChanged(nameof(VotosPorOpcion));
            }
        }
        public ICommand StartQuizCommand { get; }

        public ServerViewModel()
        {

            var ips = Dns.GetHostAddresses(Dns.GetHostName());
            IpAddress = ips
                .Where(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                .Select(x => x.ToString())
                .FirstOrDefault() ?? "0.0.0.0";
            _serverService = new ServerService(Port);
            _serverService.AnswerReceived += OnAnswerReceived;
            _serverService.QuizFinished += OnQuizFinished;
            _serverService.SendVotos += OnSendVotos;
            _serverService.SendPuntaje += OnSendPunjate;
            _questions = LoadQuestionsFromJson(Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Preguntas.json"));



            _timerEnabled = new System.Timers.Timer(5000);
            _timerEnabled.Elapsed += OnTimerEnabledElapsed;
            _timerResults = new System.Timers.Timer(3000);
            _timerResults.Elapsed += OntimerResultsElapsed;


            StartQuizCommand = new RelayCommand(StartQuiz);
            _serverView = Application.Current.MainWindow as ServerView;
        }

        private void OntimerResultsElapsed(object? sender, ElapsedEventArgs e)
        {

            _timerResults.Stop();

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_resultsView != null)
                {
                    _resultsView.Close();
                    _resultsView = null;
                }

                if (_serverView == null)
                {
                    _serverView = new ServerView { DataContext = this };
                    _serverView.Closed += (s, e) => _serverView = null;
                    _serverView.Show();

                    Application.Current.MainWindow = _serverView;
                }

                // Continuar lógica del quiz:
                //if (_currentQuestionIndex < _questions.Count)
                //{
                //    SecondsRemaining = 10;
                //    _serverService.SendQuestionAsync(_questions[_currentQuestionIndex]);
                //    _timerEnabled.Start();
                //}
            });

        }

        private void OnSendPunjate(object? sender, Dictionary<string, int> e)
        {
            UserScore = new Dictionary<string, int>(e);
        }

        private void OnSendVotos(object? sender, Dictionary<string, int> e)
        {
            if (_timerResults.Enabled)
                _timerResults.Stop();

            VotosPorOpcion = new Dictionary<string, int>(e);
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_resultsView == null)
                {
                    _resultsView = new ResultsView { DataContext = this };
                    _resultsView.Closed += (s, e) => _resultsView = null;
                    _resultsView.Show();
                    //if (_serverView != null)
                    //{

                    //    _serverView.Close();
                    //    _serverView = null;
                    //}

                    Application.Current.MainWindow = _resultsView;

                }
            });

            _timerResults.Start();

        }

        private void OnTimerEnabledElapsed(object? sender, ElapsedEventArgs e)
        {
            _timerEnabled.Stop();
            _timer = new System.Timers.Timer(1000);
            _timer.Elapsed += OnTimerElapsed;
            _serverService.EnviarMensaje("Habilitar Botones", null);
            _timer.Start();

        }

        private void OnQuizFinished(object? sender, List<RegistrationDto> clients)
        {


            foreach (var item in clients)
            {

                _serverService.SendScoresClients(item.IPAddress, item.CorrectAnswers);
            }
            Application.Current.Dispatcher.Invoke(() =>
            {

                var resultView = new ResultsView();
                resultView.DataContext = this;
                resultView.Show();

                Application.Current.MainWindow.Close();
            });

        }

        private List<QuestionModel> LoadQuestionsFromJson(string filePath)
        {
            try
            {

                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Archivo de preguntas no encontrado: {filePath}");
                }


                string json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<List<QuestionModel>>(json);
            }
            catch (Exception ex)
            {

                Console.WriteLine($"Error al cargar preguntas: {ex.Message}");
                return new List<QuestionModel>();
            }
        }

        private void StartQuiz()
        {
            _currentQuestionIndex = 0;
            SecondsRemaining = 10;
            _timerEnabled.Start();
            _serverService.SendQuestionAsync(_questions[_currentQuestionIndex]);
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (SecondsRemaining > 0)
            {
                SecondsRemaining--;
            }
            else
            {
                _timer.Stop();
                _timerEnabled.Start();
                _currentQuestionIndex++;
                if (_currentQuestionIndex < _questions.Count)
                {
                    SecondsRemaining = 10;
                    _serverService.SendQuestionAsync(_questions[_currentQuestionIndex]);
                    OnPropertyChanged();
                }
                else
                {

                    _timer.Stop();

                    _serverService.SendResultsAsync();
                }
            }
        }

        private void OnAnswerReceived(object sender, AnswerModel answer)
        {

            bool isCorrect = answer.SelectedOption == _questions[_currentQuestionIndex].CorrectAnswer;
            _serverService.UpdateUserScore(answer.UserName, isCorrect);
        }

        //private void OnQuizFinished(object sender, EventArgs e)
        //{
        //    var resulsView = new ResultsView();
        //    resulsView.Show();
        //    Application.Current.MainWindow.Close();
        //}

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}