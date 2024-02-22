using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

    public class GameManager : MonoBehaviour
    {
        public int m_NumRoundsToWin = 10;            // Número de rondas que un jugador debe ganar para ganar el juego.
        public float m_StartDelay = 3f;             // Retraso entre el inicio de las fases RoundStarting y RoundPlaying.
        public float m_EndDelay = 3f;               // Retraso entre el final de las fases RoundPlaying y RoundEnding.
        public CameraControl m_CameraControl;       // Referencia al script CameraControl para el control durante las diferentes fases.
        public Text m_MessageText, m_TimeText;                  // Referencia al Texto para mostrar mensajes de victoria, etc.
        public GameObject m_TankPrefab;             // Referencia al prefab de los tanques que los jugadores controlarán.
        public TankManager[] m_Tanks;               // Colección de managers para habilitar y deshabilitar diferentes aspectos de los tanques.

        private int m_RoundNumber;                  // Número de la ronda actual.
        private WaitForSeconds m_StartWait;         // Se utiliza para agregar un retraso al inicio de las rondas.
        private WaitForSeconds m_EndWait;           // Se utiliza para agregar un retraso al final de las rondas o del juego.
        private TankManager m_RoundWinner;          // Referencia al ganador de la ronda actual. Se utiliza para anunciar quién ganó.
        private TankManager m_GameWinner;           // Referencia al ganador del juego. Se utiliza para anunciar quién ganó.

        public float m_MaxRoundTime = 10f;
        private float m_CurrentRoundTime;

        private void Start()
        {
            // Crea los retrasos para que solo se tengan que hacer una vez.
            m_StartWait = new WaitForSeconds(m_StartDelay);
            m_EndWait = new WaitForSeconds(m_EndDelay);

            SpawnAllTanks();
            SetCameraTargets();

            // Una vez que los tanques han sido creados y la cámara los utiliza como objetivos, inicia el juego.
            StartCoroutine(GameLoop());
        }


        private void SpawnAllTanks()
        {
            // Para todos los tanques...
            for (int i = 0; i < m_Tanks.Length; i++)
            {
                // ... créalos, establece su número de jugador y referencias necesarias para el control.
                m_Tanks[i].m_Instance =
                    Instantiate(m_TankPrefab, m_Tanks[i].m_SpawnPoint.position, m_Tanks[i].m_SpawnPoint.rotation) as GameObject;
                m_Tanks[i].m_PlayerNumber = i + 1;
                m_Tanks[i].Setup();
            }
        }


        private void SetCameraTargets()
        {
            // Crea una colección de transformadas del mismo tamaño que el número de tanques.
            Transform[] targets = new Transform[m_Tanks.Length];

            // Por cada una de estas transformadas...
            for (int i = 0; i < targets.Length; i++)
            {
                // ... establece la transformada del tanque correspondiente.
                targets[i] = m_Tanks[i].m_Instance.transform;
            }

            // Estos son los objetivos que la cámara debería seguir.
            m_CameraControl.m_Targets = targets;
        }


        // Esto se llama desde el inicio y ejecutará cada fase del juego una tras otra.
        private IEnumerator GameLoop()
        {
            // Comienza ejecutando la corrutina 'RoundStarting', pero no regresa hasta que haya terminado.
            yield return StartCoroutine(RoundStarting());

            // Una vez que la corrutina 'RoundStarting' ha terminado, ejecuta la corrutina 'RoundPlaying', pero no regresa hasta que haya terminado.
            yield return StartCoroutine(RoundPlaying());

            // Una vez que la ejecución haya llegado aquí, ejecuta la corrutina 'RoundEnding', nuevamente sin regresar hasta que haya terminado.
            yield return StartCoroutine(RoundEnding());

            // Este código no se ejecuta hasta que 'RoundEnding' haya terminado. En este punto, verifica si se encontró un ganador del juego.
            if (m_GameWinner != null)
            {
                // Si hay un ganador del juego, reinicia el nivel.
                SceneManager.LoadScene(0);
            }
            else
            {
                // Si aún no hay un ganador, reinicia esta corrutina para que el bucle continúe.
                // Ten en cuenta que esta corrutina no hace 'yield', lo que significa que la versión actual de GameLoop terminará.
                StartCoroutine(GameLoop());
            }
        }


        private IEnumerator RoundStarting()
        {
            // Tan pronto como comienza la ronda, reinicia los tanques y asegúrate de que no puedan moverse.
            ResetAllTanks();
            DisableTankControl();
            // Ajusta el zoom y la posición de la cámara a algo apropiado para los tanques reiniciados.
            m_CameraControl.SetStartPositionAndSize();

            // Incrementa el número de la ronda y muestra el texto que indica a los jugadores en qué ronda están.
            m_RoundNumber++;
            m_MessageText.text = "RONDA " + m_RoundNumber;
            m_CurrentRoundTime = m_MaxRoundTime;

            // Espera durante el tiempo especificado antes de devolver el control al bucle del juego.
            yield return m_StartWait;
        }


        private IEnumerator RoundPlaying()
        {
            // As soon as the round begins playing let the players control the tanks.
            EnableTankControl();

            // Clear the text from the screen.
            m_MessageText.text = string.Empty;

            // While there is not one tank left and the round time is greater than zero...
            while (!OneTankLeft() && m_RoundNumber < 10 && m_CurrentRoundTime > 0f)
            {
                // Update the time text and decrement the current round time.
                m_TimeText.text = Mathf.Max(0, m_CurrentRoundTime).ToString("F0");
                m_CurrentRoundTime -= Time.deltaTime;

                // Wait for the next frame.
                yield return null;
            }

            // If the round time is less than or equal to zero, force both players to lose.
            if (m_CurrentRoundTime <= 0f)
            {
                m_RoundWinner = null;  // No winner in this case.
                m_GameWinner = null;   // No game winner.
                m_MessageText.text = "TIEMPO AGOTADO - AMBOS JUGADORES PIERDEN";
            }
        }


        private IEnumerator RoundEnding()
        {
            // Detén el movimiento de los tanques.
            DisableTankControl();

            // Borra al ganador de la ronda anterior.
            m_RoundWinner = null;

            // Verifica si hay un ganador ahora que la ronda ha terminado.
            m_RoundWinner = GetRoundWinner();

            // Si hay un ganador, incrementa su puntuación.
            if (m_RoundWinner != null)
                m_RoundWinner.m_Wins++;

            // Ahora que la puntuación del ganador se ha incrementado, verifica si alguien ganó el juego.
            m_GameWinner = GetGameWinner();

            // Obtiene un mensaje basado en las puntuaciones y si hay o no un ganador del juego, y lo muestra.
            string message = EndMessage(m_CurrentRoundTime <= 0f);
            m_MessageText.text = message;

            // Espera durante el tiempo especificado antes de devolver el control al bucle del juego.
            yield return m_EndWait;
        }


        // Esto se usa para verificar si queda un solo tanque y, por lo tanto, la ronda debería terminar.
        private bool OneTankLeft()
        {
            // Inicia el conteo de tanques restantes en cero.
            int numTanksLeft = 0;

            // Recorre todos los tanques...
            for (int i = 0; i < m_Tanks.Length; i++)
            {
                // ... y si están activos, incrementa el contador.
                if (m_Tanks[i].m_Instance.activeSelf)
                    numTanksLeft++;
            }

            // Si queda un solo tanque o ninguno, devuelve true, de lo contrario, devuelve false.
            return numTanksLeft <= 1;
        }


        // Esta función se utiliza para saber si hay un ganador de la ronda.
        // Se llama con la suposición de que hay 1 o menos tanques activos.
        private TankManager GetRoundWinner()
        {
            // Recorre todos los tanques...
            for (int i = 0; i < m_Tanks.Length; i++)
            {
                // ... y si uno de ellos está activo, es el ganador, así que devuélvelo.
                if (m_Tanks[i].m_Instance.activeSelf)
                    return m_Tanks[i];
            }

            // Si ninguno de los tanques está activo, es un empate, así que devuelve null.
            return null;
        }


        // Esta función se utiliza para saber si hay un ganador del juego.
        private TankManager GetGameWinner()
        {
            // Recorre todos los tanques...
            for (int i = 0; i < m_Tanks.Length; i++)
            {
                // ... y si uno de ellos tiene suficientes rondas para ganar el juego, devuélvelo.
                if (m_Tanks[i].m_Wins == m_NumRoundsToWin)
                    return m_Tanks[i];
            }

            // Si ningún tanque tiene suficientes rondas para ganar, devuelve null.
            return null;
        }


        // Devuelve un mensaje de cadena para mostrar al final de cada ronda.
        private string EndMessage(bool timeIsUp)
        {
            // De forma predeterminada, cuando una ronda termina no hay ganadores, así que el mensaje final predeterminado es un empate.
            string message = "EMPATE";

            // Si hay un ganador, cambia el mensaje para reflejar eso.
            if (m_RoundWinner != null)
                message = m_RoundWinner.m_ColoredPlayerText + " GANA LA RONDA!";

            // Añade algunos saltos de línea después del mensaje inicial.
            message += "\n\n\n\n";

            // Recorre todos los tanques y agrega cada una de sus puntuaciones al mensaje.
            for (int i = 0; i < m_Tanks.Length; i++)
            {
                message += m_Tanks[i].m_ColoredPlayerText + ": " + m_Tanks[i].m_Wins + " VICTORIAS\n";
            }

            // Si hay un ganador del juego, cambia todo el mensaje para reflejar eso.
            if (m_GameWinner != null)
                message = m_GameWinner.m_ColoredPlayerText + " GANA EL JUEGO!";

            if (timeIsUp)
                message = "TIEMPO AGOTADO - AMBOS JUGADORES PIERDEN";

            return message;
        }


        // Esta función se utiliza para volver a activar todos los tanques y restablecer sus posiciones y propiedades.
        private void ResetAllTanks()
        {
            for (int i = 0; i < m_Tanks.Length; i++)
            {
                m_Tanks[i].Reset();
            }
        }


        private void EnableTankControl()
        {
            for (int i = 0; i < m_Tanks.Length; i++)
            {
                m_Tanks[i].EnableControl();
            }
        }


        private void DisableTankControl()
        {
            for (int i = 0; i < m_Tanks.Length; i++)
            {
                m_Tanks[i].DisableControl();
            }
        }
    }
