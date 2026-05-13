using System.Windows.Controls;
using System.Windows.Media;

namespace ZoneHydrantEditor.GraphicElements
{
    public static class HydrantContextMenu
    {
        public static ContextMenu Create(Action onShowInfo,Action onEdit,Action onMove,Action onDelete,
        Action onAddBinding,Action onMoveBinding,Action onDeleteBinding,Action onStartRoute,bool hasBinding)
        {
            var menu = new ContextMenu();
            AddItem(menu, "Показать информацию", onShowInfo);
            AddItem(menu, "Редактировать информацию", onEdit);
            AddItem(menu, "Переместить гидрант", onMove);
            AddItem(menu, "Удалить гидрант", onDelete, Brushes.Red);
            AddItem(menu, "Добавить привязку", onAddBinding);
            if (hasBinding)
            {
                AddItem(menu, "Переместить привязку", onMoveBinding);
                AddItem(menu, "Удалить привязку", onDeleteBinding, Brushes.Red);
            }
            AddItem(menu, "Начать маршрут отсюда", onStartRoute);
            return menu;
        }
        private static void AddItem(ContextMenu menu, string header, Action action, Brush foreground = null)
        {
            if (action == null) return;
            var item = new MenuItem { Header = header };
            if (foreground != null) item.Foreground = foreground;
            item.Click += (s, e) => action();
            menu.Items.Add(item);
        }
    }
}