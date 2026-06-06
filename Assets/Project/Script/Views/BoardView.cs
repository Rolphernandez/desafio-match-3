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

            // --- INÍCIO DA MATEMÁTICA DE RESPONSIVIDADE ---
            RectTransform boardRect = _boardContainer.GetComponent<RectTransform>();
            float larguraDisponivel = boardRect.rect.width;

            // Pega os espaçamentos e margens para o cálculo ser 100% preciso
            float espacamentoTotal = _boardContainer.spacing.x * (board[0].Count - 1);
            float margens = _boardContainer.padding.left + _boardContainer.padding.right;

            // Calcula o tamanho exato que cada quadrado deve ter
            float tamanhoIdealDaPeca = (larguraDisponivel - espacamentoTotal - margens) / board[0].Count;

            // Aplica o tamanho dinâmico no Grid Layout
            _boardContainer.cellSize = new Vector2(tamanhoIdealDaPeca, tamanhoIdealDaPeca);
            // --- FIM DA MATEMÁTICA DE RESPONSIVIDADE ---

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
        {
            GameObject[][] tiles = new GameObject[_tiles.Length][];
            for (int y = 0; y < _tiles.Length; y++)
            {
                tiles[y] = new GameObject[_tiles[y].Length];
                for (int x = 0; x < _tiles[y].Length; x++)
                {
                    tiles[y][x] = _tiles[y][x];
                }
            }

            Sequence sequence = DOTween.Sequence();
            for (int i = 0; i < movedTiles.Count; i++)
            {
                MovedTileInfo movedTileInfo = movedTiles[i];

                Vector2Int from = movedTileInfo.From;
                Vector2Int to = movedTileInfo.To;

                sequence.Join(_tileSpots[to.y][to.x].AnimatedSetTile(_tiles[from.y][from.x]));

                tiles[to.y][to.x] = _tiles[from.y][from.x];
            }

            _tiles = tiles;

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
            if (_selectionFrame != null && _tileSpots[y] != null && _tileSpots[y][x] != null)
            {
                // Move a moldura EXATAMENTE para a posição do espaço (buraco) na tela
                _selectionFrame.transform.position = _tileSpots[y][x].transform.position;

                // Torna a moldura visível
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
