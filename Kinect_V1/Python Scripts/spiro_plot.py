import numpy as np
import matplotlib.pyplot as plt
from scipy.signal import find_peaks, firwin, filtfilt
import pandas as pd

# Paramètres du filtre
filter_order = 12  # Ordre du filtre FIR
relative_safe_margin = 0.0
max_respiratory_rate_bpm = 50  # Resp/min
frequency_cutoff_hz = max_respiratory_rate_bpm * (1 + relative_safe_margin) / 60  # Hz avec marge
sampling_rate_kinect_hz = 30  # Taux d'échantillonnage des données Kinect (à ajuster si nécessaire)

# Fonction pour supprimer les valeurs aberrantes
def remove_outliers(data):
    if len(data) < 4:  # Pas assez de données pour détecter des valeurs aberrantes
        return data
    q1 = np.percentile(data, 25)
    q3 = np.percentile(data, 75)
    iqr = q3 - q1
    lower_bound = q1 - 1.5 * iqr
    upper_bound = q3 + 1.5 * iqr
    return [x for x in data if lower_bound <= x <= upper_bound]

# Conception du filtre passe-bas FIR
filter_fir = firwin(numtaps=filter_order + 1, cutoff=frequency_cutoff_hz, fs=sampling_rate_kinect_hz)

# Variables pour tracer les données brutes en temps réel
plt.ion()
fig, ax = plt.subplots()  # Fenêtre pour les données brutes
x_data, y_data, y_filtered_data = [], [], []
line_raw, = ax.plot([], [], label='Données brutes', color='b')
line_filtered, = ax.plot([], [], label='Données filtrées', color='g')
scatter_peaks = ax.scatter([], [], color='r', label='Pics', marker='x')
scatter_troughs = ax.scatter([], [], color='y', label='Creux', marker='o')

# Configuration des axes
ax.set_xlim(0, 30)
ax.set_ylim(-2000, 2000)
ax.set_xlabel('Temps (s)')
ax.set_ylabel('Amplitude en mL')
ax.legend()

# Lire les données à partir d'un fichier CSV
# Remplacez 'data.csv' par le chemin de votre fichier CSV
dataframe = pd.read_csv("C:\\Users\\flori\\OneDrive\\Bureau\\CHU_St_Justine\\005-Trial-1-15-5-51-699.csv")
#Les valeurs sont dans la première colonne
data_values = dataframe.iloc[:, 0].values

# Variables de contrôle
flag = True
flagScale = False
scale = 0
plt.grid()

# Lecture et traitement des données en temps réel
for index, y_value in enumerate(data_values):
    if flagScale:
        scale = y_value
        print("scale:", scale)
        flagScale = False
    
    y_data.append(y_value - scale)
    x_data.append(index / 90)  # Index divisé par le taux d'échantillonnage

    # Supprimer les valeurs aberrantes
    y_data_cleaned = remove_outliers(y_data)

    # Appliquer le filtre FIR si assez de données sont présentes
    if len(y_data_cleaned) > filter_order * 5:
        y_filtered_data = filtfilt(filter_fir, 1.0, y_data_cleaned)

    # Mettre à jour les données de la ligne brute
    line_raw.set_data(x_data, y_data)

    # Mettre à jour les données filtrées
    if len(y_filtered_data) > 0:
        line_filtered.set_data(x_data[-len(y_filtered_data):], y_filtered_data)

        # Détecter les pics et creux dans les données filtrées
        y_filtered_array = np.array(y_filtered_data)
        peaks, _ = find_peaks(y_filtered_data, distance=30, width=50)
        troughs, _ = find_peaks(-y_filtered_data, distance=30, width=50)

        # Mettre à jour les coordonnées des pics et des creux
        scatter_peaks.set_offsets(np.c_[np.array(x_data)[peaks], y_filtered_data[peaks]])
        scatter_troughs.set_offsets(np.c_[np.array(x_data)[troughs], y_filtered_data[troughs]])

    plt.pause(0.01)  # Mettre à jour la figure

# Il faut débuter par une expiration et finir par une expiration
if peaks[0] < troughs[0]:
    peaks = peaks[1:]
if peaks[-1] > troughs[-1]:
    peaks = peaks[:-1]
# FR = (nombre de creux -1 )/ diviser par le temps écoulé entre le premier creux et le dernier creux multiplié par 60
FR = (len(troughs) - 1) * 60 / (x_data[troughs[-1]] - x_data[troughs[0]])

# Calcul volume courant pour chaque respiration
volume = []
for i in range(2, len(troughs) + 1, 1):
    volume.append(y_filtered_data[peaks[i - 2]] - y_filtered_data[troughs[i - 1]])
# Calcul volume courant moyen
volume_moyen = sum(volume) / len(volume)

# Calcul du volume minute expiré
volume_minute = volume_moyen * FR

# Créer une nouvelle fenêtre pour afficher la fréquence respiratoire
freq_fig, freq_ax = plt.subplots()
freq_ax.set_title('Constante respiratoire')
freq_ax.axis('off')

# Affichage de la fréquence respiratoire
freq_ax.text(0.5, 0.5, f'Fréquence respiratoire: {FR:.2f} Rpm', fontsize=15, ha='center')

# Faire une boucle pour afficher les volumes courants de chaque respiration
for i in range(len(volume)):
    freq_ax.text(0.5, 0.4 - 0.05 * (i + 1), f'Volume courant {i + 1}: {volume[i]:.2f} mL', fontsize=15, ha='center')

# Ajouter le volume courant moyen
freq_ax.text(0.5, 0.4 - 0.05 * (len(volume) + 1), f'Volume courant moyen: {volume_moyen:.2f} mL', fontsize=15, ha='center')

# Ajouter le volume minute expiré
freq_ax.text(0.5, 0.4 - 0.05 * (len(volume) + 2), f'Volume minute expiré: {volume_minute:.2f} mL/min', fontsize=15, ha='center')

plt.ioff()  # Désactiver le mode interactif
plt.show()  # Garder les fenêtres des graphiques ouvertes après la fin du script

print(y_data)
