using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;

public class FlaminFinger : MonoBehaviour {
    public KMAudio Audio;
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMSelectable ModuleSelectable;

    public KMSelectable ScreenButton;
    public Material[] LightMaterials;
    public TextMesh TimerText;
    public GameObject[] LightTiles;
    public AudioSource[] Music;

    // Module info
    private int coins = 0;

    private readonly int[] finishTiles = { 19, 20, 21, 22, 23, 24, 44, 45, 46, 47, 48, 49, 69, 70, 71, 72, 73, 74, 94, 95, 96, 97, 98, 99,
        119, 120, 121, 122, 123, 124, 144, 145, 146, 147, 148, 149 };
    private readonly int[] forbiddenTiles = { 19, 20, 21, 22, 23, 24, 44, 45, 46, 47, 48, 49, 69, 70, 71, 72, 73, 74, 94, 95, 96, 97, 98, 99,
        119, 120, 121, 122, 123, 124, 144, 145, 146, 147, 148, 149, 284, 285, 286, 287, 288, 309, 310, 311, 312, 313, 334, 335, 336, 337, 338 };

    private int[][] transitionTiles = new int[49][];

    private int[] grid = new int[625];
    private int[] bannedDirs = new int[625];
    private List<int> dirOptions = new List<int>();

    private int currentTile = 576;
    private int currentDir = 1;

    private bool canStart = false;

    private bool focused = false;
    private bool canPlay = false;
    private int selectedTile = 576;
    private int nextTile = 576;
    private int traveledTiles = 2;
    private int allMazeTiles = 0;

    private float timeLeft = 0.0f;
    private bool playTrailAnim = false;

    private int rigZone = 0;

    private static bool canPlayIntro = true;
    
    private int song = 0;

    /// Code taken from Cursor Maze
    private RaycastHit[] AllHit;
    private string[] objNames = new string[625];


    // Logging info
    private static int moduleIdCounter = 1;
    private int moduleId;
    private bool moduleSolved = false;

    // Ran as bomb loads
    private void Awake() {
        moduleId = moduleIdCounter++;
        ScreenButton.OnInteract += delegate () { PressScreen(); return false; };
        Module.OnActivate += OnActivate;

        /// Code taken from Cursor Maze
        for (int i = 0; i < objNames.Length; i++) {
            objNames[i] = "PathCM" + moduleId + "-" + i;
            LightTiles[i].name = objNames[i];
        }

        if (Application.isEditor)
            focused = true;

        ModuleSelectable.OnFocus += delegate () { focused = true; };
        ModuleSelectable.OnDefocus += delegate () { focused = false; };

        /// Code taken from Pow
        Bomb.OnBombExploded += delegate () { if (Music[song].isPlaying) Music[song].Stop(); };
    }

    // Sets up the module
    private void Start() {
        // Removes the invalid tile locations
        for (int i = 0; i < forbiddenTiles.Length; i++)
            LightTiles[forbiddenTiles[i]].SetActive(false);

        SetTransition();
    }

    // Ran as lights turn on
    private void OnActivate() {
        StartCoroutine(Startup());
    }

    // Ran as the game returns to the office
    private void OnDestroy() {
        canPlayIntro = false;
        if (Music[song].isPlaying) Music[song].Stop();
    }

    // Plays startup animation
    private IEnumerator Startup() {
        yield return new WaitForSeconds(0.5f);

        if (canPlayIntro) {
            canPlayIntro = false;
            Audio.PlaySoundAtTransform("startup_short", transform);
        }
        
        yield return new WaitForSeconds(3.0f);
        canStart = true;
    }


    // Called every frame
    private void Update() {
        if (focused && canPlay) {
            /// Code taken from Cursor Maze
            AllHit = Physics.RaycastAll(Camera.main.ScreenPointToRay(Input.mousePosition));
            List<string> names = new List<string>();

            foreach (RaycastHit hit in AllHit) {
                names.Add(hit.collider.name);
                if (objNames.Contains(hit.collider.name)) {
                    // Move to the next tile
                    if (nextTile == int.Parse(hit.collider.name.Split('-')[1])) {
                        LightTiles[selectedTile].GetComponent<Renderer>().material = LightMaterials[0];
                        selectedTile = nextTile;
                        LightTiles[selectedTile].GetComponent<Renderer>().material = LightMaterials[2];

                        switch (grid[selectedTile]) {
                            case 1: // Up
                                nextTile = selectedTile - 25;
                                break;

                            case 2: // Right
                                nextTile = selectedTile + 1;
                                break;

                            case 3: // Down
                                nextTile = selectedTile + 25;
                                break;

                            default: // Left
                                nextTile = selectedTile - 1;
                                break;
                        }

                        traveledTiles++;

                        if (traveledTiles % 2 == 1)
                            Audio.PlaySoundAtTransform("light_1", transform);

                        else
                            Audio.PlaySoundAtTransform("light_2", transform);


                        // Rigs the timer when getting close to the end
                        if ((double) traveledTiles / (double) allMazeTiles > 0.90)
                            rigZone = 2;

                        else if ((double) traveledTiles / (double) allMazeTiles > 0.75)
                            rigZone = 1;


                        // Completed the maze
                        if (traveledTiles >= allMazeTiles)
                            Solve();
                    }
                }
            }
        }

        if (!moduleSolved && canStart && coins > 0) {
            canStart = false;
            coins--;
            StartCoroutine(WipeGrid(false));
        }
    }

    // Displays the timer
    private void DisplayTime(float time) {
        time = time < 0.0f ? 0.0f : time;

        if (time < 9.9f)
            TimerText.text = string.Format("0" + "{0:F1}", time);

        else
            TimerText.text = string.Format("{0:F1}", time);
    }


    // Turns all the tiles to black
    private void Blackout() {
        TimerText.text = "";

        for (int i = 0; i < LightTiles.Length; i++) {
            LightTiles[i].GetComponent<Renderer>().material = LightMaterials[0];
        }
    }

    // Removes all red tiles from the grid
    private void RemoveRed() {
        playTrailAnim = false;

        for (int i = 0; i < LightTiles.Length; i++) {
            if (grid[i] != -1)
                LightTiles[i].GetComponent<Renderer>().material = LightMaterials[0];
        }
    }

    // Wipes the grid
    private IEnumerator WipeGrid(bool checkCoins) {
        if (checkCoins) {
            RemoveRed();
            Audio.PlaySoundAtTransform("fadeout", transform);
        }

        else
            Audio.PlaySoundAtTransform("flaminfinger", transform);

        for (int i = transitionTiles.Length - 1; i >= 0; i--) {
            for (int j = 0; j < transitionTiles[i].Length; j++)
                LightTiles[transitionTiles[i][j]].GetComponent<Renderer>().material = LightMaterials[0];

            if (i == 24)
                TimerText.text = "";

            yield return new WaitForSeconds(0.026f);
        }

        if (checkCoins && coins > 0) {
            coins--;
            GenerateMaze();
        }

        else if (!checkCoins)
            GenerateMaze();

        else {
            Debug.LogFormat("[Flamin' Finger #{0}] You ran out of coins. Flame over!", moduleId);
            canStart = true;
        }
    }


    // Generates the maze
    private void GenerateMaze() {
        Debug.LogFormat("[Flamin' Finger #{0}] Generating a new maze. You now have {1} coins.", moduleId, coins);
        ResetGrid();
        ResetBannedDirs();

        int attempts = 1;
        bool finished = false;

        while (!finished) {
            // Found the exit
            for (int i = 0; i < finishTiles.Length; i++) {
                if (currentTile == finishTiles[i]) {
                    finished = true;
                    break;
                }
            }

            if (finished)
                break;

            // Assigns the possible directions to go next
            dirOptions = null;
            dirOptions = new List<int>();

            /* 1 = Up
             * 2 = Right
             * 3 = Down
             * 4 = Left
             */

            for (int i = 1; i <= 4; i++) {
                if ((currentDir + 1) % 4 + 1 != i) { // Cannot go backwards
                    if (!GetBit(bannedDirs[currentTile], i - 1)) {
                        bool valid = true;

                        switch (i) { // Checks if the upcoming tile is occupied earlier in the maze
                            case 1:
                                if (currentTile - 50 < 0 || grid[currentTile - 50] > 0)
                                    valid = false;

                                break;

                            case 2:
                                if (currentTile + 2 > 624 || grid[currentTile + 2] > 0)
                                    valid = false;

                                break;

                            case 3:
                                if (currentTile + 50 > 624 || grid[currentTile + 50] > 0)
                                    valid = false;

                                break;

                            default:
                                if (currentTile - 2 < 0 || grid[currentTile - 2] > 0)
                                    valid = false;

                                break;
                        }

                        if (valid) {
                            dirOptions.Add(i);

                            if (currentDir == i) // Makes the maze more likely to go forward
                                dirOptions.Add(i);
                        }
                    }
                }
            }

            // Cannot choose a direction
            if (dirOptions.Count == 0) {
                // Maze generation failed
                if (bannedDirs[576] == 11 || bannedDirs[576] == 15) {
                    ResetGrid();
                    ResetBannedDirs();
                    attempts++;
                }

                else { // Goes backwards and bans that current direction
                    currentDir = grid[currentTile];
                    grid[currentTile] = 0;

                    switch (currentDir) {
                        case 1: // Up
                            grid[currentTile + 25] = 0;
                            currentTile += 50;
                            bannedDirs[currentTile]++;
                            break;

                        case 2: // Right
                            grid[currentTile - 1] = 0;
                            currentTile -= 2;
                            bannedDirs[currentTile] += 2;
                            break;

                        case 3: // Down
                            grid[currentTile - 25] = 0;
                            currentTile -= 50;
                            bannedDirs[currentTile] += 4;
                            break;

                        default: // Left
                            grid[currentTile + 1] = 0;
                            currentTile += 2;
                            bannedDirs[currentTile] += 8;
                            break;
                    }

                    // Finds the old tile's direction
                    if (grid[currentTile + 25] > 0)
                        currentDir = 1; // Up

                    else if (grid[currentTile - 1] > 0)
                        currentDir = 2; // Right

                    else if (grid[currentTile - 25] > 0)
                        currentDir = 3; // Down

                    else
                        currentDir = 4; // Left

                    grid[currentTile] = currentDir;
                }
            }

            // Moves in the grid
            else {
                currentDir = dirOptions[UnityEngine.Random.Range(0, dirOptions.Count())];
                grid[currentTile] = currentDir;

                switch (currentDir) {
                    case 1: // Up
                        grid[currentTile - 25] = 1;
                        grid[currentTile - 50] = 1;
                        currentTile -= 50;
                        break;

                    case 2: // Right
                        grid[currentTile + 1] = 2;
                        grid[currentTile + 2] = 2;
                        currentTile += 2;
                        break;

                    case 3: // Down
                        grid[currentTile + 25] = 3;
                        grid[currentTile + 50] = 3;
                        currentTile += 50;
                        break;

                    default: // Left
                        grid[currentTile - 1] = 4;
                        grid[currentTile - 2] = 4;
                        currentTile -= 2;
                        break;
                }
            }
        }

        Debug.LogFormat("[Flamin' Finger #{0}] Finished generating the maze in {1} attempt(s).", moduleId, attempts);
        CreateWalls();
        CountTiles();
        SetTime();
        StartCoroutine(DisplayMaze());
    }

    // Puts the maze on the screen
    private IEnumerator DisplayMaze() {
        switch (UnityEngine.Random.Range(0, 8)) {
            case 1:
                Audio.PlaySoundAtTransform("intro_1", transform);
                break;

            case 2:
                Audio.PlaySoundAtTransform("intro_2", transform);
                break;

            case 3:
                Audio.PlaySoundAtTransform("intro_3", transform);
                break;

            case 4:
                Audio.PlaySoundAtTransform("intro_4", transform);
                break;

            case 5:
                Audio.PlaySoundAtTransform("intro_5", transform);
                break;

            case 6:
                Audio.PlaySoundAtTransform("intro_6", transform);
                break;

            case 7:
                Audio.PlaySoundAtTransform("intro_7", transform);
                break;

            default:
                Audio.PlaySoundAtTransform("intro_8", transform);
                break;
        }

        for (int i = 0; i < transitionTiles.Length; i++) {
            for (int j = 0; j < transitionTiles[i].Length; j++)
                if (grid[transitionTiles[i][j]] == -1)
                    LightTiles[transitionTiles[i][j]].GetComponent<Renderer>().material = LightMaterials[1];

            if (i == 24)
                DisplayTime(timeLeft);

            yield return new WaitForSeconds(0.026f);
        }

        StartMaze();
    }

    // Starts the maze
    private void StartMaze() {
        selectedTile = 576;
        LightTiles[576].GetComponent<Renderer>().material = LightMaterials[2];

        switch (grid[576]) {
            case 2:
                nextTile = 577;
                break;

            default:
                nextTile = 551;
                break;
        }

        rigZone = 0;
        traveledTiles = 2;
        canPlay = true;
        StartMusic();
        StartCoroutine(StartTimer());
        StartCoroutine(StartLightTrail());
    }

    // Starts the music
    private void StartMusic() {
        song = UnityEngine.Random.Range(0, 20);

        try {
            Music[song].volume = GameMusicControl.GameMusicVolume;
        }

        catch (NullReferenceException) {
            Music[song].volume = 0.25f;
        }
        
        Music[song].Play();
    }

    // Counts down the timer
    private IEnumerator StartTimer() {
        yield return new WaitForSeconds(0.1f);

        while (timeLeft > 0.0f && !moduleSolved) {
            switch (rigZone) {
                case 2:
                    timeLeft -= 0.3f;
                    DisplayTime(timeLeft);
                    yield return new WaitForSeconds(1.0f / 60.0f); // 18x speed
                    break;

                case 1:
                    timeLeft -= 0.1f;
                    DisplayTime(timeLeft);
                    yield return new WaitForSeconds(1.0f / 30.0f); // 3x speed
                    break;

                default:
                    timeLeft -= 0.1f;
                    DisplayTime(timeLeft);
                    yield return new WaitForSeconds(0.1f); // 1x speed
                    break;
            }
            
        }

        if (!moduleSolved)
            StartCoroutine(Strike());
    }

    // Creates trails of lights
    private IEnumerator StartLightTrail() {
        playTrailAnim = true;

        while (playTrailAnim) {
            StartCoroutine(LightTrail());
            yield return new WaitForSeconds(0.1f);
        }
    }

    // Tracks the light trail
    private IEnumerator LightTrail() {
        int light = 601;

        while (light != selectedTile && playTrailAnim) {
            LightTiles[light].GetComponent<Renderer>().material = LightMaterials[2];
            yield return new WaitForSeconds(1.0f / 60.0f);
            LightTiles[light].GetComponent<Renderer>().material = LightMaterials[0];

            switch (grid[light]) {
                case 1: // Up
                    light -= 25;
                    break;

                case 2: // Right
                    light++;
                    break;

                case 3: // Down
                    light += 25;
                    break;

                default: // Left
                    light--;
                    break;
            }
        }
    }


    // Assigns the tiles for wiping transitions
    private void SetTransition() {
        transitionTiles[0] = new int[] { 600 };
        transitionTiles[1] = new int[] { 575, 601 };
        transitionTiles[2] = new int[] { 550, 576, 602 };
        transitionTiles[3] = new int[] { 525, 551, 577, 603 };
        transitionTiles[4] = new int[] { 500, 526, 552, 578, 604 };
        transitionTiles[5] = new int[] { 475, 501, 527, 553, 579, 605 };
        transitionTiles[6] = new int[] { 450, 476, 502, 528, 554, 580, 606 };
        transitionTiles[7] = new int[] { 425, 451, 477, 503, 529, 555, 581, 607 };
        transitionTiles[8] = new int[] { 400, 426, 452, 478, 504, 530, 556, 582, 608 };
        transitionTiles[9] = new int[] { 375, 401, 427, 453, 479, 505, 531, 557, 583, 609 };
        transitionTiles[10] = new int[] { 350, 376, 402, 428, 454, 480, 506, 532, 558, 584, 610 };
        transitionTiles[11] = new int[] { 325, 351, 377, 403, 429, 455, 481, 507, 533, 559, 585, 611 };
        transitionTiles[12] = new int[] { 300, 326, 352, 378, 404, 430, 456, 482, 508, 534, 560, 586, 612 };
        transitionTiles[13] = new int[] { 275, 301, 327, 353, 379, 405, 431, 457, 483, 509, 535, 561, 587, 613 };
        transitionTiles[14] = new int[] { 250, 276, 302, 328, 354, 380, 406, 432, 458, 484, 510, 536, 562, 588, 614 };
        transitionTiles[15] = new int[] { 225, 251, 277, 303, 329, 355, 381, 407, 433, 459, 485, 511, 537, 563, 589, 615 };
        transitionTiles[16] = new int[] { 200, 226, 252, 278, 304, 330, 356, 382, 408, 434, 460, 486, 512, 538, 564, 590, 616 };
        transitionTiles[17] = new int[] { 175, 201, 227, 253, 279, 305, 331, 357, 383, 409, 435, 461, 487, 513, 539, 565, 591, 617 };
        transitionTiles[18] = new int[] { 150, 176, 202, 228, 254, 280, 306, 332, 358, 384, 410, 436, 462, 488, 514, 540, 566, 592, 618 };
        transitionTiles[19] = new int[] { 125, 151, 177, 203, 229, 255, 281, 307, 333, 359, 385, 411, 437, 463, 489, 515, 541, 567, 593, 619 };
        transitionTiles[20] = new int[] { 100, 126, 152, 178, 204, 230, 256, 282, 308, 360, 386, 412, 438, 464, 490, 516, 542, 568, 594, 620 };
        transitionTiles[21] = new int[] { 75, 101, 127, 153, 179, 205, 231, 257, 283, 361, 387, 413, 439, 465, 491, 517, 543, 569, 595, 621 };
        transitionTiles[22] = new int[] { 50, 76, 102, 128, 154, 180, 206, 232, 258, 362, 388, 414, 440, 466, 492, 518, 544, 570, 596, 622 };
        transitionTiles[23] = new int[] { 25, 51, 77, 103, 129, 155, 181, 207, 233, 259, 363, 389, 415, 441, 467, 493, 519, 545, 571, 597, 623 };
        transitionTiles[24] = new int[] { 0, 26, 52, 78, 104, 130, 156, 182, 208, 234, 260, 364, 390, 416, 442, 468, 494, 520, 546, 572, 598, 624 };
        transitionTiles[25] = new int[] { 1, 27, 53, 79, 105, 131, 157, 183, 209, 235, 261, 339, 365, 391, 417, 443, 469, 495, 521, 547, 573, 599 };
        transitionTiles[26] = new int[] { 2, 28, 54, 80, 106, 132, 158, 184, 210, 236, 262, 314, 340, 366, 392, 418, 444, 470, 496, 522, 548, 574 };
        transitionTiles[27] = new int[] { 3, 29, 55, 81, 107, 133, 159, 185, 211, 237, 263, 289, 315, 341, 367, 393, 419, 445, 471, 497, 523, 549 };
        transitionTiles[28] = new int[] { 4, 30, 56, 82, 108, 134, 160, 186, 212, 238, 264, 290, 316, 342, 368, 394, 420, 446, 472, 498, 524 };
        transitionTiles[29] = new int[] { 5, 31, 57, 83, 109, 135, 161, 187, 213, 239, 265, 291, 317, 343, 369, 395, 421, 447, 473, 499 };
        transitionTiles[30] = new int[] { 6, 32, 58, 84, 110, 136, 162, 188, 214, 240, 266, 292, 318, 344, 370, 396, 422, 448, 474 };
        transitionTiles[31] = new int[] { 7, 33, 59, 85, 111, 137, 163, 189, 215, 241, 267, 293, 319, 345, 371, 397, 423, 449 };
        transitionTiles[32] = new int[] { 8, 34, 60, 86, 112, 138, 164, 190, 216, 242, 268, 294, 320, 346, 372, 398, 424 };
        transitionTiles[33] = new int[] { 9, 35, 61, 87, 113, 139, 165, 191, 217, 243, 269, 295, 321, 347, 373, 399 };
        transitionTiles[34] = new int[] { 10, 36, 62, 88, 114, 140, 166, 192, 218, 244, 270, 296, 322, 348, 374 };
        transitionTiles[35] = new int[] { 11, 37, 63, 89, 115, 141, 167, 193, 219, 245, 271, 297, 323, 349 };
        transitionTiles[36] = new int[] { 12, 38, 64, 90, 116, 142, 168, 194, 220, 246, 272, 298, 324 };
        transitionTiles[37] = new int[] { 13, 39, 65, 91, 117, 143, 169, 195, 221, 247, 273, 299 };
        transitionTiles[38] = new int[] { 14, 40, 66, 92, 118, 170, 196, 222, 248, 274 };
        transitionTiles[39] = new int[] { 15, 41, 67, 93, 171, 197, 223, 249 };
        transitionTiles[40] = new int[] { 16, 42, 68, 172, 198, 224 };
        transitionTiles[41] = new int[] { 17, 43, 173, 199 };
        transitionTiles[42] = new int[] { 18, 174 };
        transitionTiles[43] = new int[] {  };
        transitionTiles[44] = new int[] {  };
        transitionTiles[45] = new int[] {  };
        transitionTiles[46] = new int[] {  };
        transitionTiles[47] = new int[] {  };
        transitionTiles[48] = new int[] {  };
    }

    // Resets the grid
    private void ResetGrid() {
        for (int i = 0; i < grid.Length; i++)
            grid[i] = 0;

        grid[601] = 1;
        grid[576] = 1;

        currentTile = 576;
        currentDir = 1;
    }

    // Resets the list for invalid direction movements
    private void ResetBannedDirs() {
        for (int i = 0; i < bannedDirs.Length; i++) {
            bannedDirs[i] = 0;
        }

        /* 1 = Up
         * 2 = Right
         * 4 = Down
         * 8 = Left
         */

        bannedDirs[26] = 9;
        bannedDirs[28] = 1;
        bannedDirs[30] = 1;
        bannedDirs[32] = 1;
        bannedDirs[34] = 1;
        bannedDirs[36] = 1;
        bannedDirs[38] = 1;
        bannedDirs[40] = 1;
        bannedDirs[42] = 1;
        bannedDirs[198] = 2;
        bannedDirs[248] = 2;
        bannedDirs[298] = 2;
        bannedDirs[348] = 2;
        bannedDirs[398] = 2;
        bannedDirs[448] = 2;
        bannedDirs[498] = 2;
        bannedDirs[548] = 2;
        bannedDirs[598] = 6;
        bannedDirs[596] = 4;
        bannedDirs[594] = 4;
        bannedDirs[592] = 4;
        bannedDirs[590] = 4;
        bannedDirs[588] = 4;
        bannedDirs[586] = 4;
        bannedDirs[584] = 4;
        bannedDirs[582] = 4;
        bannedDirs[580] = 4;
        bannedDirs[578] = 4;
        bannedDirs[576] = 8;
        bannedDirs[526] = 8;
        bannedDirs[476] = 8;
        bannedDirs[426] = 8;
        bannedDirs[376] = 8;
        bannedDirs[326] = 8;
        bannedDirs[276] = 8;
        bannedDirs[226] = 8;
        bannedDirs[176] = 8;
        bannedDirs[126] = 8;
        bannedDirs[76] = 8;
        bannedDirs[234] = 4;
        bannedDirs[236] = 4;
        bannedDirs[238] = 4;
        bannedDirs[290] = 8;
        bannedDirs[340] = 8;
        bannedDirs[388] = 1;
        bannedDirs[386] = 1;
        bannedDirs[384] = 1;
        bannedDirs[332] = 2;
        bannedDirs[282] = 2;
    }


    // Gets the bit from the parsed integer - https://www.reddit.com/r/csharp/comments/mx14gk/getting_bits_from_an_integer/
    private bool GetBit(int bannedDirs, int dir) {
        return (bannedDirs & (1 << dir)) != 0;
    }

    // Assigns the walls in the maze
    private void CreateWalls() {
        for (int i = 26; i <= 598; i++) {
            if (grid[i] > 0) {
                grid[i - 26] = grid[i - 26] == 0 ? -1 : grid[i - 26];
                grid[i - 25] = grid[i - 25] == 0 ? -1 : grid[i - 25];
                grid[i - 24] = grid[i - 24] == 0 ? -1 : grid[i - 24];
                grid[i - 1] = grid[i - 1] == 0 ? -1 : grid[i - 1];
                grid[i + 1] = grid[i + 1] == 0 ? -1 : grid[i + 1];
                grid[i + 24] = grid[i + 24] == 0 ? -1 : grid[i + 24];
                grid[i + 25] = grid[i + 25] == 0 ? -1 : grid[i + 25];
                grid[i + 26] = grid[i + 26] == 0 ? -1 : grid[i + 26];
            }
        }

        // Removes walls on the finishing area
        for (int i = 0; i < finishTiles.Length; i++)
            grid[finishTiles[i]] = 0;
    }

    // Gets the number of travelable tiles in the maze
    private void CountTiles() {
        allMazeTiles = 0;

        for (int i = 0; i < grid.Length; i++) {
            if (grid[i] > 0)
                allMazeTiles++;
        }
    }

    // Gets the allotted time for the maze
    private void SetTime() {
        timeLeft = allMazeTiles * 0.2f;
        Debug.LogFormat("[Flamin' Finger #{0}] You have {1} seconds. Get your flame on!", moduleId, string.Format("{0:F1}", timeLeft));
    }


    // Press screen
    private void PressScreen() {
        ScreenButton.AddInteractionPunch(0.25f);

        if (!moduleSolved) {
            Audio.PlaySoundAtTransform("insertcoin", transform);
            coins++;
            Debug.LogFormat("[Flamin' Finger #{0}] You inserted a coin. Now at {1}.", moduleId, coins);
        }
    }


    // Module solves
    private void Solve() {
        canPlay = false;
        playTrailAnim = false;
        moduleSolved = true;
        Blackout();

        if (Music[song].isPlaying) Music[song].Stop();
        Audio.PlaySoundAtTransform("jackpot_short", transform);
        Debug.LogFormat("[Flamin' Finger #{0}] Congratulations! You won the jackpot!", moduleId);
        GetComponent<KMBombModule>().HandlePass();
    }

    // Module strikes
    private IEnumerator Strike() {
        canPlay = false;
        Debug.LogFormat("[Flamin' Finger #{0}] Time's up! You couldn\'t complete the maze!", moduleId);
        GetComponent<KMBombModule>().HandleStrike();

        if (Music[song].isPlaying) Music[song].Stop();
        if (rigZone == 2) {
            switch (UnityEngine.Random.Range(0, 8)) {
                case 1:
                    Audio.PlaySoundAtTransform("buzzerend_1", transform);
                    break;

                case 2:
                    Audio.PlaySoundAtTransform("buzzerend_2", transform);
                    break;

                case 3:
                    Audio.PlaySoundAtTransform("buzzerend_3", transform);
                    break;

                case 4:
                    Audio.PlaySoundAtTransform("buzzerend_4", transform);
                    break;

                case 5:
                    Audio.PlaySoundAtTransform("buzzerend_5", transform);
                    break;

                case 6:
                    Audio.PlaySoundAtTransform("buzzerend_6", transform);
                    break;

                case 7:
                    Audio.PlaySoundAtTransform("buzzerend_7", transform);
                    break;

                default:
                    Audio.PlaySoundAtTransform("buzzerend_8", transform);
                    break;
            }
        }

        else
            Audio.PlaySoundAtTransform("buzzer", transform);

        yield return new WaitForSeconds(2.5f);
        StartCoroutine(WipeGrid(true));
    }
}