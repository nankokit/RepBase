using RepBase.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace RepBase.Services
{
    public class ScriptService
    {
        private readonly DatabaseManager _databaseManager;
        private readonly Dictionary<string, string> _scripts;
        private readonly ObservableCollection<string> _scriptNames;

        public ObservableCollection<string> ScriptNames => _scriptNames;

        public ScriptService(DatabaseManager databaseManager)
        {
            _databaseManager = databaseManager;
            _scripts = new Dictionary<string, string>();
            _scriptNames = new ObservableCollection<string>();
            InitializeScripts();
        }

        private void InitializeScripts()
        {
            // Загружаем предустановленные скрипты из default_scripts.json
            LoadDefaultScripts();

            // Загружаем пользовательские скрипты из user_scripts.json
            LoadUserScripts();

            // Обновляем коллекцию ScriptNames
            _scriptNames.Clear();
            foreach (var scriptName in _scripts.Keys)
            {
                _scriptNames.Add(scriptName);
            }
        }

        private void LoadDefaultScripts()
        {
            try
            {
                if (File.Exists("default_scripts.json"))
                {
                    var json = File.ReadAllText("default_scripts.json");
                    var defaultScripts = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (defaultScripts != null)
                    {
                        foreach (var script in defaultScripts)
                        {
                            _scripts[script.Key] = script.Value;
                        }
                    }
                }
                else
                {
                    MessageBox.Show("File 'default_scripts.json' not found. Default scripts will not be loaded.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading default scripts: {ex.Message}");
            }
        }

        private void LoadUserScripts()
        {
            try
            {
                if (File.Exists("user_scripts.json"))
                {
                    var json = File.ReadAllText("user_scripts.json");
                    var userScripts = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (userScripts != null)
                    {
                        foreach (var script in userScripts)
                        {
                            // Пользовательские скрипты перезаписывают предустановленные
                            _scripts[script.Key] = script.Value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading user scripts: {ex.Message}");
            }
        }

        private void SaveScriptsToFile()
        {
            try
            {
                // Сохраняем только пользовательские скрипты (исключаем предустановленные)
                var userScripts = _scripts
                    .Where(s => !_scripts.ContainsKey(s.Key) || !IsDefaultScript(s.Key))
                    .ToDictionary(s => s.Key, s => s.Value);
                var json = JsonSerializer.Serialize(userScripts, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText("user_scripts.json", json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving scripts: {ex.Message}");
            }
        }

        private bool IsDefaultScript(string scriptName)
        {
            // Проверяем, является ли скрипт предустановленным
            try
            {
                if (File.Exists("default_scripts.json"))
                {
                    var json = File.ReadAllText("default_scripts.json");
                    var defaultScripts = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    return defaultScripts != null && defaultScripts.ContainsKey(scriptName);
                }
            }
            catch
            {
                // Если не удалось прочитать default_scripts.json, считаем, что скрипт не предустановленный
            }
            return false;
        }

        public string GetScriptContent(string scriptName)
        {
            return _scripts.ContainsKey(scriptName) ? _scripts[scriptName] : "";
        }

        public DataTable ExecuteScript(string script)
        {
            if (string.IsNullOrWhiteSpace(script))
            {
                MessageBox.Show("Введите SQL-скрипт для выполнения.");
                return null;
            }

            try
            {
                var result = _databaseManager.ExecuteQuery(script);
                if (result == null || result.Rows.Count == 0)
                {
                    MessageBox.Show("Скрипт выполнен, но не вернул данных.");
                    return null;
                }
                return result;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error executing script: {ex.Message}");
                return null;
            }
        }

        public void SaveScript(string script, string scriptName)
        {
            if (string.IsNullOrWhiteSpace(script))
            {
                MessageBox.Show("Введите SQL-скрипт для сохранения.");
                return;
            }

            if (_scripts.ContainsKey(scriptName) && scriptName != "новый скрипт")
            {
                var result = MessageBox.Show($"Скрипт с именем '{scriptName}' уже существует. Перезаписать?",
                    "Подтверждение", MessageBoxButton.YesNo);
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            _scripts[scriptName] = script;
            if (!_scriptNames.Contains(scriptName))
            {
                _scriptNames.Add(scriptName);
            }
            SaveScriptsToFile();
            MessageBox.Show($"Скрипт '{scriptName}' успешно сохранен.");
        }
    }
}