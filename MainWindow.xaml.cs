using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace FP{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow{
        private ArrayList hours = new ArrayList();
        private Grid parent;
        private Grid template;
        private Grid bottomControls;

        public MainWindow() {
            InitializeComponent();
        }

        // Sets the bottom control variable
        private void SetBottomControls(object sender, EventArgs e) => bottomControls = (Grid) sender;

        // Gets the structure template for the input grid
        private void GetInputGrid(object sender, EventArgs e) {
            var grid = (Grid) sender;

            parent = ((Grid) grid.Parent);
            template = GetGridDetails(grid, true); //creates the template
            parent.Children.Remove(grid); //removes the structure template
        }

        // Checks to see if a row needs to be removed or a new row needs to be added when the text content changes in a field
        private void NameChange(object sender, EventArgs e) {
            var tb = (TextBox) sender;
            var ptb = ((Grid)tb.Parent);
            var children = GetBaseRows();

            if (IsEmpty(tb.Text)) {
                // check if all other siblings (non hour siblings) are also empty
                var any = GetChildrenWhere<TextBox>(ptb, it => (it.Name == "client" || it.Name == "contract" || it.Name == "project") && it.Text.Trim() != "").Any();

                if (!any && children.Count() > 1){
                    parent.Children.Remove(ptb); //if no other text sibling has content, remove this row

                    // check each day to find if any of it's sub fields have hours logged, if not, re-enable the checkbox for that day
                    foreach (var cb in bottomControls.Children.OfType<CheckBox>()) {
                        var day = cb.Name.Replace("_checkbox", "");
                        foreach(var grid in children)
                            if (IsEmpty(GetChildrenWhere<TextBox>(grid, it => it.Name == day).First().Text)) {
                                cb.IsEnabled = true;
                                break;
                            }
                    }
                    // Repositions each row
                    foreach (var grid in children)
                        grid.Margin = new Thickness(grid.Margin.Left, grid.Margin.Top - 28, 0, 0);
                }
            } else {
                // check if parent is last grid in list OR the number of children is greater than 15 (15 because 
                // more than 5 rows went beyond what I think should be allowed, it can be changed if necessary
                // without affecting the logic behind the application).
                if (ptb == children.Last() && children.Count() < 15) AddNewInputGrid();
                children = GetBaseRows();
            }

            bottomControls.Margin = new Thickness(bottomControls.Margin.Left, children.Last().Margin.Top + 28, 0, 0);
        }
        
        // Used to copy the template grid
        private Grid GetGridDetails(Grid template, bool IsFirst = false) {
            // Gets the offset of the last base row element, used to position the new element
            var offsetTop = GetBaseRows().Last().Margin.Top;

            // Creates a new Grid using properties from the template
            var newGrid = new Grid {
                Margin = new Thickness(template.Margin.Left, offsetTop + (IsFirst ? 0 : 28), 0, 0),
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left,
                Name = "Base_Row"
            };

            // Adds the text fields and assigns the appropriate event handler based on the name of the field
            foreach (var c in template.Children) {
                var tb = new TextBox {
                    Margin = ((TextBox) c).Margin,
                    Width = ((TextBox) c).Width,
                    Height = ((TextBox) c).Height,
                    Name = ((TextBox) c).Name,
                };

                switch (tb.Name){
                    case "client":
                    case "contract":
                    case "project":
                        tb.TextChanged += NameChange;
                        break;
                    default:
                        tb.Text = "0";
                        tb.TextChanged += ValidateFieldHours;
                        break;
                }

                newGrid.Children.Add(tb); //adds field to new grid
            }
            parent.Children.Add(newGrid);
            return newGrid;
        }

        // Calculates the payroll information
        private void CalculatePayment(object sender, RoutedEventArgs e) {
            var daysLabel = ((Label) parent.FindName("All_Days"));
            var was = ((Label) parent.FindName("WeekSupervisor"));
            var label = ((Label) parent.FindName("Under40"));
            var week = (TextBox) parent.FindName("Week_Field");
            var super = (TextBox) parent.FindName("Supervisor_Field");
            var result = (GroupBox) parent.FindName("Results");

            // Hides all the warning labels/payroll information
            if (daysLabel != null)
                daysLabel.Visibility = Visibility.Hidden;
            if (label != null)
                label.Visibility = Visibility.Hidden;
            if (result != null)
                result.Visibility = Visibility.Hidden;
            if (was != null) {
                was.Visibility = Visibility.Hidden;
                if (IsEmpty(week.Text) || IsEmpty(super.Text)) {
                    was.Visibility = Visibility.Visible;
                    return;
                }
            }

            try {
                var vacationDays = GetTotalVacationDays(); // get by counting number of checked checkboxes
                var totalHours = GetTotalHoursWorked(vacationDays);

                var maxHours = 40;
                // the total hours, minus the max hours (40) will either result in a positive
                // number (meaning there was overtime), or a negative number (no overtime).
                // If overtime is a negative number, is is made a 0 and calculations proceed.
                var overtimeHours = Math.Max(totalHours - maxHours, 0);
                // The total hours minus the overtimeHours (which will either be positive or
                // zero) results in the regular hours worked.
                var regHours = totalHours - overtimeHours;
                var hourlyPay = 15;

                var rhp = regHours * hourlyPay;
                var ohp = overtimeHours * (hourlyPay * 1.5);

                // Makes the "missing information" label visible
                if (label != null && regHours < maxHours)
                    label.Visibility = Visibility.Visible;

                // sets all the values for the payroll information
                ((Label) result.FindName("week_result")).Content = week.Text;
                ((Label) result.FindName("supervisor_result")).Content = super.Text;
                ((Label) result.FindName("reg_hours_result")).Content = regHours;
                ((Label) result.FindName("overtime_result")).Content = overtimeHours;
                ((Label) result.FindName("reg_hours_pay_result")).Content = rhp;
                ((Label) result.FindName("overtime_pay_result")).Content = ohp;
                ((Label) result.FindName("total")).Content = rhp + ohp;
                ((Label) result.FindName("vacation")).Content = vacationDays;

                // makes the payroll information visible
                if (result != null)
                    result.Visibility = Visibility.Visible;
            } catch(Exception _) {
                if(daysLabel != null)
                    daysLabel.Visibility = Visibility.Visible;
            }
        }

        // Gets the total number of checkboxes selected
        private int GetTotalVacationDays() => GetChildrenWhere<CheckBox>(bottomControls, it => it.IsChecked == true).Count();

        // Gets the total hours worked for the current week
        private int GetTotalHoursWorked(int vacationDays) {
            var texts = new string[] { "client", "contract", "project" };
            var sum = new Dictionary<string, int>() {
                ["sunday"] = -1,
                ["monday"] = -1,
                ["tuesday"] = -1,
                ["wednesday"] = -1,
                ["thursday"] = -1,
                ["friday"] = -1,
                ["saturday"] = -1
            };

            foreach(var grid in GetBaseRows())
                foreach (var tb in GetChildrenWhere<TextBox>(grid, it => !texts.Contains(it.Name)))
                    if (!IsEmpty(tb.Text))
                        sum[tb.Name] = (sum[tb.Name] >= 0? sum[tb.Name] : 0) + int.Parse(tb.Text);
            
            if (sum.Where(it => it.Value > 0).Count() < 7 - vacationDays)
                throw new Exception("Cannot leave days empty");

            return sum.Sum(it => it.Value);
        }

        // Validates an object's content to make sure it is a number
        private void ValidateNumber(object sender, bool isWeek) {
            var tb = (TextBox) sender;
            var content = tb.Text;
            
            // Removes any non numeric characters
            if (Regex.IsMatch(content, "[^0-9]")) 
                content = tb.Text.ToCharArray().Where(char.IsDigit).Aggregate("", (current, c) => current + c);
            
            CheckBox cb = null;
            if (!isWeek)
                cb = GetChildrenWhere<CheckBox>(bottomControls, it => it.Name == tb.Name + "_checkbox").First();

            if (IsEmpty(content)) content = "0";

            // If the content is empty, renable the checkbox (cb is null if it is a week field)
            if (content == "0") {
                if (cb != null) cb.IsEnabled = true;
                tb.Text = "0";
                return;
            }
            
            // If there is input, the checkbox is both disabled and set to false, cb is null if it is a week field.
            if (cb != null) {
                cb.IsEnabled = false;
                cb.IsChecked = false;
            }
            var remainingHoursForDay = 24; // TODO: need to calculate this based on the hours already enter by user

            foreach (var grid in GetBaseRows().Where(it => it != tb.Parent))
                foreach (var ctb in GetChildrenWhere<TextBox>(grid, it => !IsEmpty(it.Text) && it.Name == tb.Name))
                    remainingHoursForDay -= int.Parse(ctb.Text);

            // Content is min/maxed to being between 0 and 24 (or 52 if is week field), technically
            // the zero isn't necessary because the minus sign isn't valid input, but
            // there's no harm in having it there either.
            tb.Text = Math.Max(Math.Min(int.Parse(content), isWeek ? 52 : remainingHoursForDay), 0).ToString();
        }

        // Ensures that the week feild is both a positive number and under 52
        private void ValidateFieldWeek(object sender, TextChangedEventArgs e) => ValidateNumber(sender, true);
        // Ensures that the hour field is both a number and can be set to that high of a value
        private void ValidateFieldHours(object sender, TextChangedEventArgs e) => ValidateNumber(sender, false);

        //Shortcut for adding a new grid based on the template
        private void AddNewInputGrid() => GetGridDetails(template);
        
        // Checks if string is empty or contains just white space
        public static bool IsEmpty(String s) => s.Trim().Length == 0;

        //Shortcut for getting base row grids
        private IEnumerable<Grid> GetBaseRows() => GetChildrenWhere<Grid>(parent, it => it.Name == "Base_Row");
        // Shortcut for Children.OfType<T>.Where
        private IEnumerable<T> GetChildrenWhere<T>(Grid element, Func<T, bool> func) => element.Children.OfType<T>().Where(func);
    }
}

/*
- display calculations
*/