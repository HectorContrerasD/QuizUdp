using GalaSoft.MvvmLight.Command;
using Newtonsoft.Json;
using Server.Models;
using Server.Models.DTOs;
using Server.Services;

using System.ComponentModel;
using System.IO;
using System.Net;
using System.Timers;
using System.Windows.Input;


namespace Server.ViewModels
{
    public class ServerViewModel : INotifyPropertyChanged
    {

        private ServerService _serverService;
        private List<QuestionModel> _questions = new();
        private int _currentQuestionIndex;
        private int _secondsRemaining;
        private System.Timers.Timer _timer; 
        private Dictionary<string, int> _userScores = new Dictionary<string, int>();

        public string IpAddress { get; }
        public int Port { get; } = 5000;

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
            _questions = LoadQuestionsFromJson(Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Preguntas.json"));
            


            _timer = new System.Timers.Timer(1000); 
            _timer.Elapsed += OnTimerElapsed;


            StartQuizCommand = new RelayCommand(StartQuiz);
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
            _timer.Start();
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

        private void OnQuizFinished(object sender, EventArgs e)
        {

        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}