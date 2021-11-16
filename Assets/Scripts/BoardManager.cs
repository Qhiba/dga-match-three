﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoardManager : MonoBehaviour
{
    #region Singleton
    private static BoardManager _instance = null;
    public static BoardManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<BoardManager>();

                if (_instance == null)
                {
                    Debug.LogError("Fatal Error: BoardManager not Found");
                }
            }

            return _instance;
        }
    }
    #endregion

    public bool IsAnimating
    {
        get
        {
            return IsProcessing || IsSwapping;
        }
    }
    public bool IsProcessing { get; set; }
    public bool IsSwapping { get; set; }

    [Header("Board")]
    public Vector2Int size;
    public Vector2 offsetTile;
    public Vector2 offsetBoard;

    [Header("Tile")]
    public List<Sprite> tileTypes = new List<Sprite>();
    public GameObject tilePrefab;

    private Vector2 startPosition;
    private Vector2 endPosition;
    private TileController[,] tiles;

    private int combo;

    // Start is called before the first frame update
    void Start()
    {
        Vector2 tileSize = tilePrefab.GetComponent<SpriteRenderer>().size;
        CreateBoard(tileSize);
    }

    public void Process()
    {
        combo = 0;
        IsProcessing = true;
        ProcessMatches();
    }

    private void CreateBoard(Vector2 tileSize)
    {
        tiles = new TileController[size.x, size.y];
        Vector2 totalSize = (tileSize + offsetTile) * (size - Vector2.one);

        startPosition = (Vector2)transform.position - (totalSize / 2) + offsetBoard;
        endPosition = startPosition + totalSize;

        Vector2 distanceBetweenTile;
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                distanceBetweenTile.x = (tileSize.x + offsetTile.x) * x;
                distanceBetweenTile.y = (tileSize.y + offsetTile.y) * y;
                Vector2 tilePosition = startPosition + distanceBetweenTile;

                TileController newTile = Instantiate(tilePrefab, tilePosition, tilePrefab.transform.rotation, transform).GetComponent<TileController>();
                tiles[x, y] = newTile;

                //Get no tile id
                List<int> possibleId = GetStartingPossibleIdList(x, y);
                int newId = possibleId[Random.Range(0, possibleId.Count)];

                newTile.ChangeId(newId, x, y);
            }
        }
    }

    private List<int> GetStartingPossibleIdList(int x, int y)
    {
        List<int> possibleId = new List<int>();

        for (int i = 0; i < tileTypes.Count; i++)
        {
            possibleId.Add(i);
        }

        if (x > 1 && tiles[x-1, y].id == tiles[x-2, y].id)
        {
            possibleId.Remove(tiles[x - 1, y].id);
        }

        if (y > 1 && tiles[x, y-1].id == tiles[x, y-2].id)
        {
            possibleId.Remove(tiles[x, y - 1].id);
        }

        return possibleId;
    }

    #region Swapping
    public IEnumerator SwapTilePosition(TileController a, TileController b, System.Action onCompleted)
    {
        IsSwapping = true;

        Vector2Int indexA = GetTileIndex(a);
        Vector2Int indexB = GetTileIndex(b);

        tiles[indexA.x, indexA.y] = b;
        tiles[indexB.x, indexB.y] = a;

        a.ChangeId(a.id, indexB.x, indexB.y);
        b.ChangeId(b.id, indexA.x, indexA.y);

        bool isRoutineACompleted = false;
        bool isRoutineBCompleted = false;

        StartCoroutine(a.MoveTilePosition(GetIndexPosition(indexB), () => { isRoutineACompleted = true; }));
        StartCoroutine(b.MoveTilePosition(GetIndexPosition(indexA), () => { isRoutineBCompleted = true; }));

        yield return new WaitUntil(() => { return isRoutineACompleted && isRoutineBCompleted; });

        onCompleted?.Invoke();
        IsSwapping = false;
    }
    #endregion

    public Vector2Int GetTileIndex(TileController tile)
    {
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                if (tile == tiles[x,y])
                {
                    return new Vector2Int(x, y);
                }
            }
        }

        return new Vector2Int(-1, -1);
    }

    public Vector2 GetIndexPosition(Vector2Int index)
    {
        Vector2 tileSize = tilePrefab.GetComponent<SpriteRenderer>().size;
        return new Vector2(startPosition.x + ((tileSize.x + offsetTile.x) * index.x), startPosition.y + ((tileSize.y + offsetTile.y) * index.y));
    }

    public List<TileController> GetAllMatches()
    {
        Debug.Log("GetAllMatches Start");

        List<TileController> matchingTiles = new List<TileController>();

        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                List<TileController> tileMatched = tiles[x, y].GetAllMatches();

                //Just go to next tile if no match
                if (tileMatched == null || tileMatched.Count == 0)
                {
                    continue;
                }

                foreach (TileController item in tileMatched)
                {
                    //Add only the one that is not added yet
                    if (!matchingTiles.Contains(item))
                    {
                        matchingTiles.Add(item);
                    }
                }
            }
        }

        Debug.Log("GetAllMatches Finish");

        return matchingTiles;
    }

    #region Match
    private void ProcessMatches()
    {
        Debug.Log("ProcessMatches Start");
        List<TileController> matchingTiles = GetAllMatches();

        //Stop locking if no match found
        if (matchingTiles == null || matchingTiles.Count == 0)
        {
            IsProcessing = false;
            return;
        }

        combo++;
        ScoreManager.Instance.IncrementCurrentScore(matchingTiles.Count, combo);

        StartCoroutine(ClearMatches(matchingTiles, ProcessDrop));
    }

    private IEnumerator ClearMatches(List<TileController> matchingTiles, System.Action onCompleted)
    {
        Debug.Log("ClearMatches Start");

        List<bool> isCompleted = new List<bool>();

        for (int i = 0; i < matchingTiles.Count; i++)
        {
            isCompleted.Add(false);
        }

        for (int i = 0; i < matchingTiles.Count; i++)
        {
            int index = i;
            StartCoroutine(matchingTiles[i].SetDestroyed(() => { isCompleted[index] = true; }));
        }

        yield return new WaitUntil(() => { return IsAllTrue(isCompleted); });

        Debug.Log("ClearMatches Finish");

        onCompleted?.Invoke();
    }
    #endregion

    public bool IsAllTrue(List<bool> list)
    {
        foreach (bool status in list)
        {
            Debug.Log("Status = " + status);
            if (!status)
            {
                return false;
            }
        }

        return true;
    }

    #region Drop
    private void ProcessDrop()
    {
        Debug.Log("ProcessDrop Start");

        Dictionary<TileController, int> droppingTiles = GetAllDrop();

        StartCoroutine(DropTiles(droppingTiles, ProcessDestroyAndFill));
    }

    private Dictionary<TileController, int> GetAllDrop()
    {
        Debug.Log("GetAllDrop Start");

        Dictionary<TileController, int> droppingTiles = new Dictionary<TileController, int>();

        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                if (tiles[x, y].IsDestroyed)
                {
                    //Process for all tile on top of destroyed tlie
                    for (int i = y + 1; i < size.y; i++)
                    {
                        if (tiles[x, i].IsDestroyed)
                        {
                            continue;
                        }

                        //If this tile already on drop list, increase its drop range
                        if (droppingTiles.ContainsKey(tiles[x, i]))
                        {
                            droppingTiles[tiles[x, i]]++;
                        }
                        //If not on drop list, add it with drop range one
                        else
                        {
                            droppingTiles.Add(tiles[x, i], 1);
                        }
                    }
                }
            }
        }

        Debug.Log("GetAllDrop Finish");

        return droppingTiles;
    }

    private IEnumerator DropTiles(Dictionary<TileController, int> droppingTiles, System.Action onComplete)
    {
        Debug.Log("DropTiles Start");

        foreach (KeyValuePair<TileController, int> pair in droppingTiles)
        {
            Vector2Int tileIndex = GetTileIndex(pair.Key);

            TileController temp = pair.Key;
            tiles[tileIndex.x, tileIndex.y] = tiles[tileIndex.x, tileIndex.y - pair.Value];
            tiles[tileIndex.x, tileIndex.y - pair.Value] = temp;

            temp.ChangeId(temp.id, tileIndex.x, tileIndex.y - pair.Value);
        }

        yield return null;

        Debug.Log("DropTiles Finish");

        onComplete?.Invoke();
    }
    #endregion

    #region Destroy & Fill
    private void ProcessDestroyAndFill()
    {
        Debug.Log("ProcessDestroyAndFill Start");

        List<TileController> destroyedTiles = GetAllDestroyed();

        StartCoroutine(DestroyAndFillTiles(destroyedTiles, ProcessReposition));
    }

    private List<TileController> GetAllDestroyed()
    {
        Debug.Log("GetAllDestroyed Start");

        List<TileController> destroyedTiles = new List<TileController>();

        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                if (tiles[x,y].IsDestroyed)
                {
                    destroyedTiles.Add(tiles[x, y]);
                }
            }
        }

        Debug.Log("GetAllDestroyed Finish");

        return destroyedTiles;
    }

    private IEnumerator DestroyAndFillTiles(List<TileController> destoryedTiles, System.Action onComplete)
    {
        Debug.Log("DestroyAndFillTiles Start");

        List<int> highestIndex = new List<int>();

        for (int i = 0; i < size.x; i++)
        {
            highestIndex.Add(size.y - 1);
        }

        float spawnHeight = endPosition.y + tilePrefab.GetComponent<SpriteRenderer>().size.y + offsetTile.y;

        foreach (TileController tile in destoryedTiles)
        {
            Vector2Int tileIndex = GetTileIndex(tile);
            Vector2Int targetIndex = new Vector2Int(tileIndex.x, highestIndex[tileIndex.x]);
            highestIndex[tileIndex.x]--;

            tile.transform.position = new Vector2(tile.transform.position.x, spawnHeight);
            tile.GenerateRandomTile(targetIndex.x, targetIndex.y);
        }

        yield return null;

        Debug.Log("DestroyAndFillTiles Finish");

        onComplete?.Invoke();
    }
    #endregion

    #region Reposition
    private void ProcessReposition()
    {
        Debug.Log("ProcessReposition Start");

        StartCoroutine(RepositionTiles(ProcessMatches));
    }

    private IEnumerator RepositionTiles(System.Action onCompeleted)
    {
        Debug.Log("RepositionTiles Start");

        List<bool> isCompleted = new List<bool>();

        int i = 0;
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                Vector2 targetPosition = GetIndexPosition(new Vector2Int(x, y));

                //Skip if already on position
                if ((Vector2)tiles[x,y].transform.position == targetPosition)
                {
                    continue;
                }

                isCompleted.Add(false);

                int index = i;
                StartCoroutine(tiles[x, y].MoveTilePosition(targetPosition, () => { isCompleted[index] = true; }));

                i++;
            }
        }

        yield return new WaitUntil(() => { return IsAllTrue(isCompleted); });

        Debug.Log("RepositionTiles Finish");

        onCompeleted?.Invoke();
    }
    #endregion
}
