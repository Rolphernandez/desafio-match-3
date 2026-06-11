using System;
using System.Collections.Generic;
using DG.Tweening;
using Gazeus.DesafioMatch3.Core;
using Gazeus.DesafioMatch3.Models;
using Gazeus.DesafioMatch3.Views;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

namespace Gazeus.DesafioMatch3.Controllers
{
    public class GameController : MonoBehaviour
    {
        [SerializeField] private BoardView _boardView;
        [SerializeField] private int _boardHeight = 10;
        [SerializeField] private int _boardWidth = 10;

        private GameService _gameService;
        private bool _isAnimating;
        private int _selectedX = -1;
        private int _selectedY = -1;

        [Header("UI Elements")]
        [SerializeField] private GameObject _menuScreen;
        [SerializeField] private GameObject _gameplayScreen;
        [SerializeField] private TMP_Text _timerText;
        [SerializeField] private TMP_Text _coinText;

        [Header("Game Settings")]
        [SerializeField] private float _timeLimit = 60f; // Tempo em segundos

        private float _timeRemaining;
        private int _currentCoins = 0;
        private bool _isGameRunning = false;

        [Header("Progress Bar Settings")]
        [SerializeField] private UnityEngine.UI.Image _progressImage; 

        private bool _bonus80Gained = false;
        private bool _bonus180Gained = false;

        [Header("Audio Settings")]
        [SerializeField] private AudioSource _bgmAudioSource; // Arraste a sua música para cá
        [SerializeField] private float _normalPitch = 1.0f;
        [SerializeField] private float _fastPitch = 1.25f; // Ajuste aqui o quão rápido quer a música

        [Header("Bonus Visual References")]
        [SerializeField] private GameObject _iceBonus1;
        [SerializeField] private ParticleSystem _particleBonus1;

        [SerializeField] private GameObject _iceBonus2;
        [SerializeField] private ParticleSystem _particleBonus2;

        [Header("Bonus Screen VFX")]
        [SerializeField] private ParticleSystem _timeBonusVFX; // Arraste o Time_vfx aqui
        [SerializeField] private ParticleSystem _coinBonusVFX; // Arraste o Coin_vfx aqui

        private bool _isMusicSpedUp = false;

        [Header("Screen Center Combos (Juice)")]
        [SerializeField] private ParticleSystem _greatComboParticle;    // Para 4 peças
        [SerializeField] private ParticleSystem _perfectComboParticle;  // Para 5 ou mais peças

        [Header("Game Over Screen")]
        [SerializeField] private GameObject _gameOverScreen;
        [SerializeField] private UnityEngine.UI.Button _btnGo;
        [SerializeField] private TMPro.TMP_Text _finalScoreText; // Referência temporária para o texto do "3º lugar" Somente para simular o Ranked.

        #region Unity
        private void Awake()
        {
            _gameService = new GameService();
            _boardView.TileClicked += OnTileClick;
            
            if (_gameOverScreen != null) _gameOverScreen.SetActive(false);

            if (_btnGo != null)
            {
                _btnGo.onClick.AddListener(RestartGame);
            }
        }

        private void OnDestroy()
        {
            _boardView.TileClicked -= OnTileClick;

            if (_btnGo != null)
            {
                _btnGo.onClick.RemoveListener(RestartGame);
            }
        }

        private void Start()
        {
            List<List<Tile>> board = _gameService.StartGame(_boardWidth, _boardHeight);
            _boardView.CreateBoard(board);
        }
        #endregion

        

        private void AnimateBoard(List<BoardSequence> boardSequences, int index, Action onComplete)
        {
            BoardSequence boardSequence = boardSequences[index];

            if (boardSequence.MatchedPosition != null && boardSequence.MatchedPosition.Count > 0)
            {
                int matchCount = boardSequence.MatchedPosition.Count;

                // 1. Contabiliza moedas e atualiza progresso
                _currentCoins += matchCount;
                UpdateCoinUI();
                UpdateProgressBar();
                CheckProgressMilestones();

                // --- SISTEMA DE COMBOS NO MEIO DA TELA COM ÁUDIO ---
                if (matchCount == 4)
                {
                    if (_greatComboParticle != null)
                    {
                        _greatComboParticle.Play();

                        // Toca o som do GREAT se houver um AudioSource no objeto
                        if (_greatComboParticle.TryGetComponent<AudioSource>(out var audioGreat))
                        {
                            audioGreat.Play();
                        }
                    }
                    
                }
                else if (matchCount >= 5)
                {
                    if (_perfectComboParticle != null)
                    {
                        _perfectComboParticle.Play();

                        // Toca o som do PERFECT se houver um AudioSource no objeto
                        if (_perfectComboParticle.TryGetComponent<AudioSource>(out var audioPerfect))
                        {
                            audioPerfect.Play();
                        }
                    }
                    
                }
                // ---------------------------------------------------------------
            }

            Sequence sequence = DOTween.Sequence();
            sequence.Append(_boardView.DestroyTiles(boardSequence.MatchedPosition));
            sequence.Append(_boardView.MoveTiles(boardSequence.MovedTiles));
            sequence.Append(_boardView.CreateTile(boardSequence.AddedTiles));

            index += 1;
            if (index < boardSequences.Count)
            {
                sequence.onComplete += () => AnimateBoard(boardSequences, index, onComplete);
            }
            else
            {
                sequence.onComplete += () => onComplete();
            }
        }

        private void OnTileClick(int x, int y)
        {
            if (_isAnimating) return;

            if (_selectedX > -1 && _selectedY > -1)
            {
                if (Mathf.Abs(_selectedX - x) + Mathf.Abs(_selectedY - y) > 1)
                {
                    // Clicou longe: apenas atualiza as coordenadas e move a moldura para a nova peça
                    _selectedX = x;
                    _selectedY = y;
                    _boardView.ShowSelectionFrame(x, y);
                }
                else
                {
                    _isAnimating = true;

                    // Vai tentar o merge: esconde a moldura imediatamente
                    _boardView.HideSelectionFrame();

                    _boardView.SwapTiles(_selectedX, _selectedY, x, y).onComplete += () =>
                    {
                        bool isValid = _gameService.IsValidMovement(_selectedX, _selectedY, x, y);
                        if (isValid)
                        {
                            List<BoardSequence> swapResult = _gameService.SwapTile(_selectedX, _selectedY, x, y);
                            AnimateBoard(swapResult, 0, () => _isAnimating = false);
                        }
                        else
                        {
                            // Merge falhou, faz o bate e volta
                            _boardView.SwapTiles(x, y, _selectedX, _selectedY).onComplete += () => _isAnimating = false;
                        }
                        _selectedX = -1;
                        _selectedY = -1;
                    };
                }
            }
            else
            {
                _selectedX = x;
                _selectedY = y;

                // Primeiro clique: mostra a moldura em cima da peça clicada
                _boardView.ShowSelectionFrame(x, y);
            }
        }

        // Função que será chamada pelo Botão Play
        public void StartGameSequence()
        {
            _menuScreen.SetActive(false);
            _gameplayScreen.SetActive(true);

            _timeRemaining = _timeLimit;
            _currentCoins = 0;
            _isGameRunning = true;

            UpdateCoinUI();

            _bonus80Gained = false;
            _bonus180Gained = false;
            if (_progressImage != null) _progressImage.fillAmount = 0f;
           
            _isMusicSpedUp = false;
            if (_bgmAudioSource != null)
                _bgmAudioSource.pitch = _normalPitch;

            // Garante que as imagens do gelo estejam visíveis ao reiniciar
            if (_iceBonus1 != null)
            {
                _iceBonus1.SetActive(true);
                if (_iceBonus1.TryGetComponent<UnityEngine.UI.Image>(out var img1)) img1.enabled = true;
            }
            if (_iceBonus2 != null)
            {
                _iceBonus2.SetActive(true);
                if (_iceBonus2.TryGetComponent<UnityEngine.UI.Image>(out var img2)) img2.enabled = true;
            }
        }

        // O Update roda a cada frame do jogo
        private void Update()
        {
            if (_isGameRunning && _timeRemaining > 0)
            {
                _timeRemaining -= Time.deltaTime;
                UpdateTimerUI();

                // --- SISTEMA DINÂMICO DE VELOCIDADE DA MÚSICA ---
                if (_timeRemaining <= 10f)
                {
                    // Se o tempo for menor que 10 e a música ainda estiver normal, acelera!
                    if (!_isMusicSpedUp)
                    {
                        _isMusicSpedUp = true;
                        if (_bgmAudioSource != null) _bgmAudioSource.pitch = _fastPitch;
                        
                    }
                }
                else
                {
                    // Se o tempo for maior que 10 (ex: ganhou bônus) e a música estava acelerada, volta ao normal!
                    if (_isMusicSpedUp)
                    {
                        _isMusicSpedUp = false;
                        if (_bgmAudioSource != null) _bgmAudioSource.pitch = _normalPitch;
                        
                    }
                }
                // -------------------------------------------------

                // --- FIM DE JOGO (UNIFICADO) ---
                if (_timeRemaining <= 0)
                {
                    _timeRemaining = 0;
                    _isGameRunning = false;

                    // Para a música
                    if (_bgmAudioSource != null) _bgmAudioSource.Stop();

                    // Atualiza a pontuação final na UI
                    if (_finalScoreText != null)
                    {
                        _finalScoreText.text = _currentCoins.ToString();
                    }

                    // Mostra a tela de Game Over
                    if (_gameOverScreen != null)
                    {
                        _gameOverScreen.SetActive(true);
                    }

                    
                }
                // -------------------------------
            }
        }

        private void UpdateTimerUI()
        {
            if (_timerText != null)
            {
                // Mathf.CeilToInt arredonda para cima (ex: 59.1 vira 60)
                _timerText.text = Mathf.CeilToInt(_timeRemaining).ToString();
            }
        }

        private void UpdateCoinUI()
        {
            if (_coinText != null)
            {
                _coinText.text = _currentCoins.ToString();
            }
        }

        private void UpdateProgressBar()
        {
            if (_progressImage != null)
            {
                // O Mathf.Clamp01 garante que o valor nunca passe de 1.0 (100%), mesmo se o jogador passar de 500 pontos
                _progressImage.fillAmount = Mathf.Clamp01((float)_currentCoins / 500f);
            }
        }
        private void CheckProgressMilestones()
        {
            // 1º Bônus: 80 pontos -> Ganha +20 segundos de tempo
            if (_currentCoins >= 80 && !_bonus80Gained)
            {
                _bonus80Gained = true;
                _timeRemaining += 20f;
                UpdateTimerUI();

                // Quebra o gelo pequeno da barra
                ShatterIce(_iceBonus1, _particleBonus1);

                // --- NOVO: Dispara o VFX Gigante de Tempo no meio da tela ---
                if (_timeBonusVFX != null)
                {
                    _timeBonusVFX.Play();
                    if (_timeBonusVFX.TryGetComponent<AudioSource>(out var audioTime))
                    {
                        audioTime.Play();
                    }
                }
                // -----------------------------------------------------------

                
            }

            // 2º Bônus: 180 pontos -> Ganha +100 moedas de bônus instantâneas
            if (_currentCoins >= 180 && !_bonus180Gained)
            {
                _bonus180Gained = true;
                _currentCoins += 100;

                UpdateCoinUI();
                UpdateProgressBar();

                // Quebra o gelo pequeno da barra
                ShatterIce(_iceBonus2, _particleBonus2);

                // --- NOVO: Dispara o VFX Gigante de Moedas no meio da tela ---
                if (_coinBonusVFX != null)
                {
                    _coinBonusVFX.Play();
                    if (_coinBonusVFX.TryGetComponent<AudioSource>(out var audioCoin))
                    {
                        audioCoin.Play();
                    }
                }
                // ------------------------------------------------------------

                
            }
        }

        private void ShatterIce(GameObject iceObject, ParticleSystem particle)
        {
            if (iceObject == null) return;

            // 1. Desativa apenas o componente de imagem para o gelo sumir visualmente na hora
            if (iceObject.TryGetComponent<UnityEngine.UI.Image>(out var iceImage))
            {
                iceImage.enabled = false;
            }

            // 2. Toca o efeito de estilhaços de vidro/gelo
            if (particle != null)
            {
                particle.Play();

                // --- NOVA LÓGICA: Encontra o AudioSource da partícula e toca o som ---
                if (particle.TryGetComponent<AudioSource>(out var particleAudio))
                {
                    particleAudio.Play();
                }
                // ---------------------------------------------------------------------

                // 3. Usa o DOTween para desativar o GameObject por completo após a duração do efeito (ex: 1.5 segundos)
                DG.Tweening.DOVirtual.DelayedCall(1.5f, () => {
                    iceObject.SetActive(false);
                });
            }
            else
            {
                // Caso não tenha partícula configurada, desativa direto
                iceObject.SetActive(false);
            }
        }
        public void RestartGame()
        {
            // Isso recarrega a cena atual do zero, limpando o tabuleiro e resetando todas as variáveis perfeitamente!
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
