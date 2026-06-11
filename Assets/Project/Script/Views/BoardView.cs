using System;
using System.Collections.Generic;
using DG.Tweening;
using Gazeus.DesafioMatch3.Models;
using Gazeus.DesafioMatch3.ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace Gazeus.DesafioMatch3.Views
{
    public class BoardView : MonoBehaviour
    {
        public event Action<int, int> TileClicked;

        [SerializeField] private GridLayoutGroup _boardContainer;
        [SerializeField] private TilePrefabRepository _tilePrefabRepository;
        [SerializeField] private TileSpotView _tileSpotPrefab;
        [SerializeField] private GameObject _selectionFrame;
        [SerializeField] private GameObject _matchParticlePrefab;

        private GameObject[][] _tiles;
        private TileSpotView[][] _tileSpots;

        public void CreateBoard(List<List<Tile>> board)
        {
            // Define a quantidade de colunas do Grid
            _boardContainer.constraintCount = board[0].Count;

            // Define a quantidade de colunas do Grid
            _boardContainer.constraintCount = board[0].Count;

            // --- MATEMÁTICA DE RESPONSIVIDADE (LARGURA VS ALTURA) ---
            RectTransform boardRect = _boardContainer.GetComponent<RectTransform>();

            // Pegamos o espaço total do contêiner do tabuleiro
            float larguraDisponivel = boardRect.rect.width;
            float alturaDisponivel = boardRect.rect.height;

            // Descontamos apenas o espaçamento (Spacing) entre as peças
            float espacamentoTotalX = _boardContainer.spacing.x * (board[0].Count - 1);
            float espacamentoTotalY = _boardContainer.spacing.y * (board.Count - 1);

            // Calculamos o tamanho máximo que a carta pode ter para não estourar a LARGURA
            float tamanhoMaximoLargura = (larguraDisponivel - espacamentoTotalX) / board[0].Count;

            // Calculamos o tamanho máximo que a carta pode ter para não estourar a ALTURA (10 linhas)
            float tamanhoMaximoAltura = (alturaDisponivel - espacamentoTotalY) / board.Count;

            // A MÁGICA: Escolhemos o MENOR tamanho. 
            // Se a tela for muito fina, a carta encolhe baseada na largura.
            // Se a tela for muito baixa (ou tiver muitas linhas), ela encolhe baseada na altura!
            float tamanhoIdeal = Mathf.Min(tamanhoMaximoLargura, tamanhoMaximoAltura);

            // Aplica o tamanho perfeito e quadrado nas cartas
            _boardContainer.cellSize = new Vector2(tamanhoIdeal, tamanhoIdeal);
            // ---------------------------------------------------------

            _tiles = new GameObject[board.Count][];
            _tileSpots = new TileSpotView[board.Count][];

            for (int y = 0; y < board.Count; y++)
            {
                _tiles[y] = new GameObject[board[0].Count];
                _tileSpots[y] = new TileSpotView[board[0].Count];

                for (int x = 0; x < board[0].Count; x++)
                {
                    TileSpotView tileSpot = Instantiate(_tileSpotPrefab);
                    tileSpot.transform.SetParent(_boardContainer.transform, false);
                    tileSpot.SetPosition(x, y);
                    tileSpot.Clicked += TileSpot_Clicked;

                    _tileSpots[y][x] = tileSpot;

                    int tileTypeIndex = board[y][x].Type;
                    if (tileTypeIndex > -1)
                    {
                        GameObject tilePrefab = _tilePrefabRepository.TileTypePrefabList[tileTypeIndex];
                        GameObject tile = Instantiate(tilePrefab);
                        tileSpot.SetTile(tile);

                        _tiles[y][x] = tile;
                    }
                }
            }
        }

        public Tween CreateTile(List<AddedTileInfo> addedTiles)
        {
            Sequence sequence = DOTween.Sequence();
            for (int i = 0; i < addedTiles.Count; i++)
            {
                AddedTileInfo addedTileInfo = addedTiles[i];
                Vector2Int position = addedTileInfo.Position;

                TileSpotView tileSpot = _tileSpots[position.y][position.x];

                GameObject tilePrefab = _tilePrefabRepository.TileTypePrefabList[addedTileInfo.Type];
                GameObject tile = Instantiate(tilePrefab);
                tileSpot.SetTile(tile);

                _tiles[position.y][position.x] = tile;

                tile.transform.localScale = Vector2.zero;
                sequence.Join(tile.transform.DOScale(1.0f, 0.2f));
            }

            return sequence;
        }

        public Tween DestroyTiles(List<Vector2Int> matchedPosition)
        {
            for (int i = 0; i < matchedPosition.Count; i++)
            {
                Vector2Int position = matchedPosition[i];

                if (_tiles[position.y][position.x] != null)
                {
                    // 1. Pega a posição e força um pequeno ajuste no eixo Z (-5) 
                    // Isso puxa a partícula um pouco para "frente" em direção à câmera, garantindo que não entre atrás do tabuleiro
                    Vector3 posicaoDaPeca = _tiles[position.y][position.x].transform.position;
                    posicaoDaPeca.z -= 5f;

                    if (_matchParticlePrefab != null)
                    {
                        GameObject particula = Instantiate(_matchParticlePrefab, posicaoDaPeca, Quaternion.identity);

                        // 2. Coloca no Canvas, MAS usa 'false' no final. 
                        // Isso impede que a partícula herde as posições matemáticas loucas do RectTransform
                        particula.transform.SetParent(_boardContainer.transform, false);

                        // 3. Força a posição novamente para ter certeza absoluta
                        particula.transform.position = posicaoDaPeca;

                        // 4. MULTIPLICA A ESCALA: Se a partícula 3D ficar minúscula no Canvas, 
                        // você deve forçar um tamanho gigante aqui. Se ficar muito grande, mude o 50f para 10f ou 5f.
                        particula.transform.localScale = Vector3.one;
                    }

                    Destroy(_tiles[position.y][position.x]);
                    _tiles[position.y][position.x] = null;
                }
            }

            return DOVirtual.DelayedCall(0.2f, () => { });
        }

        public Tween MoveTiles(List<MovedTileInfo> movedTiles)
        {Sequence sequence = DOTween.Sequence();

            // Lista temporária para processar as movimentações sem sobrescrever dados antes da hora
            List<(Vector2Int to, GameObject tile)> pendingMoves = new List<(Vector2Int, GameObject)>();
            for (int i = 0; i < movedTiles.Count; i++)
            { MovedTileInfo movedTileInfo = movedTiles[i];
                Vector2Int from = movedTileInfo.From;
                Vector2Int to = movedTileInfo.To;

                GameObject tileToMove = _tiles[from.y][from.x];
                sequence.Join(_tileSpots[to.y][to.x].AnimatedSetTile(tileToMove));

                pendingMoves.Add((to, tileToMove));

                // Limpa a posição antiga se ela não foi o destino de outro movimento neste mesmo frame
                if (_tiles[from.y][from.x] == tileToMove)
                {
                    _tiles[from.y][from.x] = null;
                }
            }

            // Aplica as novas posições de forma segura na matriz original existente
            foreach (var move in pendingMoves)
            {
                _tiles[move.to.y][move.to.x] = move.tile;
            }

            return sequence;
        }

        public Tween SwapTiles(int fromX, int fromY, int toX, int toY)
        {
            Sequence sequence = DOTween.Sequence();
            sequence.Append(_tileSpots[fromY][fromX].AnimatedSetTile(_tiles[toY][toX]));
            sequence.Join(_tileSpots[toY][toX].AnimatedSetTile(_tiles[fromY][fromX]));

            (_tiles[toY][toX], _tiles[fromY][fromX]) = (_tiles[fromY][fromX], _tiles[toY][toX]);

            return sequence;
        }

        public void ShowSelectionFrame(int x, int y)
        {
            // Verifica se a moldura e o spot existem para evitar erros no console
            if (_selectionFrame != null && _tileSpots[y] != null && _tileSpots[y][x] != null)
            {
                // 1. Mantém a sua lógica de posição (está correta!)
                _selectionFrame.transform.position = _tileSpots[y][x].transform.position;

                // 2. ADICIONA A SINCRONIZAÇÃO DE TAMANHO:
                // Pegamos o RectTransform do "buraco" na grade e da moldura
                RectTransform spotRect = _tileSpots[y][x].GetComponent<RectTransform>();
                RectTransform frameRect = _selectionFrame.GetComponent<RectTransform>();

                if (spotRect != null && frameRect != null)
                {
                    // A moldura assume exatamente a mesma largura e altura do spot
                    frameRect.sizeDelta = spotRect.sizeDelta;
                }

                // 3. Torna a moldura visível
                _selectionFrame.SetActive(true);
            }
        }

        public void HideSelectionFrame()
        {
            if (_selectionFrame != null)
            {
                // Esconde a moldura
                _selectionFrame.SetActive(false);
            }
        }

        #region Events
        private void TileSpot_Clicked(int x, int y)
        {
            TileClicked(x, y);
        }
        #endregion
    }
}
