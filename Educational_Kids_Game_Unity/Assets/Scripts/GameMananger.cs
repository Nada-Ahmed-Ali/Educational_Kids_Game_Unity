using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class GameMananger : MonoBehaviour
{
    [Header("Game Elements")]
    [Range(2, 6)]
    [SerializeField] private int difficulty = 4;
    [SerializeField] private Transform gameHolder;
    [SerializeField] private Transform piecePrefab;

    [Header("UI Elements")]
    [SerializeField] private List<Texture2D> imageTextures;
    [SerializeField] private Transform levelSelectPanel;
    [SerializeField] private Image levelSelectPrefab;
    [SerializeField] private GameObject playAgainButton;

    private List<Transform> pieces;
    private Vector2Int dimensions;
    private float width;
    private float height;

    private Transform draggingPiece = null;
    private Vector3 offset;

    private int piecesCorrect;

    void Start()
    {
        // Create the UI
        foreach (Texture2D texture in imageTextures)
        {
            Image image = Instantiate(levelSelectPrefab, levelSelectPanel);
            image.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);
            // Assign button action
            image.GetComponent<Button>().onClick.AddListener(delegate { StartGame(texture); });
        }
    }

    public void StartGame(Texture2D jigsawTexture)
    {
        // Hide the UI
        levelSelectPanel.gameObject.SetActive(false);

        // We store a list of the transform for each jigsaw piece so we can track them later.
        pieces = new List<Transform>();

        // Calculate the size of each jigsaw piece, based on a difficulty setting.
        dimensions = GetDimensions(jigsawTexture, difficulty);

        // Create the pieces of the correct size with the correct texture
        CreateJigsawPieces(jigsawTexture);

        Scatter();

        UpdateBorder();

        piecesCorrect = 0;
    }

    Vector2Int GetDimensions(Texture2D jigsawTexture, int difficulty)
    {
        Vector2Int dimensions = Vector2Int.zero;
        // Difficulty is the number of pieces on the smallest texture dimension.
        // This helps ensure the pieces are as square as possible.
        if (jigsawTexture.width < jigsawTexture.height)
        {
            dimensions.x = difficulty;
            dimensions.y = (difficulty * jigsawTexture.height) / jigsawTexture.width;
        }
        else
        {
            dimensions.x = (difficulty * jigsawTexture.width) / jigsawTexture.height;
            dimensions.y = difficulty;
        }
        return dimensions;
    }

    // Create all the jigsaw pieces
    void CreateJigsawPieces(Texture2D jigsawTexture)
    {
        // Calculate piece sizes based on the dimensions.
        height = 1f / dimensions.y;
        float aspect = (float)jigsawTexture.width / jigsawTexture.height;
        width = aspect / dimensions.x;

        for (int row = 0; row < dimensions.y; row++)
        {
            for (int col = 0; col < dimensions.x; col++)
            {
                // Create the piece in the right location of the right size.
                Transform piece = Instantiate(piecePrefab, gameHolder);
                piece.localPosition = new Vector3(
                  (-width * dimensions.x / 2) + (width * col) + (width / 2),
                  (-height * dimensions.y / 2) + (height * row) + (height / 2),
                  -1);
                piece.localScale = new Vector3(width, height, 1f);

                // We don't have to name them, but always useful for debugging.
                piece.name = $"Piece {(row * dimensions.x) + col}";
                pieces.Add(piece);

                // Assign the correct part of the texture for this jigsaw piece
                // We need our width and height both to be normalised between 0 and 1 for the UV.
                float width1 = 1f / dimensions.x;
                float height1 = 1f / dimensions.y;
                // UV coord order is anti-clockwise: (0, 0), (1, 0), (0, 1), (1, 1)
                Vector2[] uv = new Vector2[4];
                uv[0] = new Vector2(width1 * col, height1 * row);
                uv[1] = new Vector2(width1 * (col + 1), height1 * row);
                uv[2] = new Vector2(width1 * col, height1 * (row + 1));
                uv[3] = new Vector2(width1 * (col + 1), height1 * (row + 1));
                // Assign our new UVs to the mesh.

                Mesh mesh = piece.GetComponent<MeshFilter>().mesh = Instantiate(
                    piece.GetComponent<MeshFilter>().sharedMesh
                );

                mesh.uv = uv;
                // Update the texture on the piece

                MeshRenderer mr = piece.GetComponent<MeshRenderer>();
                mr.material.mainTexture = jigsawTexture;
                mr.material.color = Color.white;


            }
        }
    }

    public void Scatter()
    {
        float orthHeight = Camera.main.orthographicSize;
        float screenAspect = (float)Screen.width / Screen.height;
        float orthWidth = (screenAspect * orthHeight);

        float pieceWidth = width * gameHolder.localScale.x;
        float pieceHeight = height * gameHolder.localScale.y;

        orthHeight -= pieceHeight;
        orthWidth -= pieceWidth;

        foreach(Transform piece in pieces)
        {
            float x = Random.Range(-orthWidth, orthWidth);
            float y = Random.Range(-orthHeight, orthHeight);
            piece.position = new Vector3(x, y, -1);
        }
    }

    private void UpdateBorder()
    {
        LineRenderer lineRenderer = gameHolder.GetComponent<LineRenderer>();

        float halfWidth = (width * dimensions.x) / 2f;
        float halfHeight = (height * dimensions.y) / 2f;

        float borderZ = 0f;

        lineRenderer.SetPosition(0, new Vector3(-halfWidth, halfHeight, borderZ));
        lineRenderer.SetPosition(1, new Vector3(halfWidth, halfHeight, borderZ));
        lineRenderer.SetPosition(2, new Vector3(halfWidth, -halfHeight, borderZ));
        lineRenderer.SetPosition(3, new Vector3(-halfWidth, -halfHeight, borderZ));

        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;

        lineRenderer.enabled = true;

    }

    void Update()
    {
        if (Pointer.current == null)
            return;

        Vector2 screenPos = Pointer.current.position.ReadValue();
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(screenPos);
        worldPos.z = -1f;

        // PRESS
        if (Pointer.current.press.wasPressedThisFrame)
        {
            RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero);

            if (hit && pieces.Contains(hit.transform))
            {
                draggingPiece = hit.transform;
                offset = draggingPiece.position - worldPos;
            }
        }

        // DRAG
        if (draggingPiece && Pointer.current.press.isPressed)
        {
            draggingPiece.position = worldPos + offset;
        }

        // RELEASE
        if (draggingPiece && Pointer.current.press.wasReleasedThisFrame)
        {
            SnapAndDisableIfCorrect();
            draggingPiece = null;
        }

    }

    private void SnapAndDisableIfCorrect()
    {
        int pieceIndex = pieces.IndexOf(draggingPiece);
        if (pieceIndex < 0) return;

        int col = pieceIndex % dimensions.x;
        int row = pieceIndex / dimensions.x;

        Vector2 targetPosition = new Vector2(
            (-width * dimensions.x / 2f) + (width * col) + (width / 2f),
            (-height * dimensions.y / 2f) + (height * row) + (height / 2f)
        );

        if (Vector2.Distance(draggingPiece.localPosition, targetPosition) < width / 2f)
        {
            draggingPiece.localPosition = targetPosition;
            draggingPiece.GetComponent<BoxCollider2D>().enabled = false;

            piecesCorrect++;
            if (piecesCorrect == pieces.Count)
            {
                playAgainButton.SetActive(true);
            }
        }
    }

    public void RestartGame()
    {
        // Destroy all the puzzle pieces.
        foreach (Transform piece in pieces)
        {
            Destroy(piece.gameObject);
        }
        pieces.Clear();
        // Hide the outline
        gameHolder.GetComponent<LineRenderer>().enabled = false;
        // Show the level select UI.
        playAgainButton.SetActive(false);
        levelSelectPanel.gameObject.SetActive(true);
    }

}